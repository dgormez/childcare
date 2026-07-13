using ChildCare.Application.Common;
using ChildCare.Application.Staff;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.RoomShifts;

/// <summary>
/// FR-010: select-then-PIN check-out — tap your own (checked-in) card, confirm PIN. Feature
/// 008b: when the location's RequiresCaregiverPin is false, the tap alone completes check-out —
/// see CheckInCommand's doc comment for the shared reasoning, including why a null/empty client
/// pin must be coerced to empty string (never passed through as null) when the location does
/// require PIN verification.
/// </summary>
public record CheckOutCommand(Guid LocationId, Guid StaffId, string? Pin) : IRequest<CheckOutResult>;

public class CheckOutCommandHandler(VerifyPinCommand verifyPin, CloseStaleShiftsHelper closeStaleShifts, ITenantDbContext db)
    : IRequestHandler<CheckOutCommand, CheckOutResult>
{
    public async Task<CheckOutResult> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        await closeStaleShifts.CloseStaleShiftsAsync(request.LocationId, DateTime.UtcNow, cancellationToken);

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        var pinToVerify = location is { RequiresCaregiverPin: false } ? null : request.Pin ?? string.Empty;

        var verification = await verifyPin.VerifyAsync(request.LocationId, request.StaffId, pinToVerify, cancellationToken);
        if (!verification.Succeeded)
        {
            return verification.Failure switch
            {
                PinVerificationFailure.NotEligible => CheckOutResult.NotEligible(),
                PinVerificationFailure.Invalid => CheckOutResult.Invalid(verification.AttemptsRemaining!.Value),
                PinVerificationFailure.Locked => CheckOutResult.Locked(verification.LockedUntil!.Value),
                _ => throw new InvalidOperationException($"Unhandled {nameof(PinVerificationFailure)}: {verification.Failure}"),
            };
        }

        var openShift = await db.RoomShifts.FirstOrDefaultAsync(
            s => s.StaffProfileId == request.StaffId && s.LocationId == request.LocationId && s.CheckedOutAt == null,
            cancellationToken);
        if (openShift is null)
            return CheckOutResult.NotCheckedIn();

        var now = DateTime.UtcNow;
        openShift.CheckedOutAt = now;
        await db.SaveChangesAsync(cancellationToken);

        var profile = verification.StaffProfile!;
        return CheckOutResult.Success(new CheckOutResponse(profile.Id, profile.FirstName, now));
    }
}
