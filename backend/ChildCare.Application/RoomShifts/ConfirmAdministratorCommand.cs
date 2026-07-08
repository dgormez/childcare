using ChildCare.Application.Common;
using ChildCare.Application.Staff;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.RoomShifts;

/// <summary>
/// FR-017/018: sensitive-action administrator confirmation — the same select-then-PIN pattern
/// as check-in/out, narrowed on the client to the currently-checked-in roster. The server
/// independently re-checks "currently checked in" (FR-017) regardless of what the client's UI
/// would normally prevent selecting.
/// </summary>
public record ConfirmAdministratorCommand(Guid LocationId, Guid? StaffId, string? Pin, bool Skip) : IRequest<ConfirmAdministratorResult>;

public class ConfirmAdministratorCommandHandler(VerifyPinCommand verifyPin, ITenantDbContext db)
    : IRequestHandler<ConfirmAdministratorCommand, ConfirmAdministratorResult>
{
    public async Task<ConfirmAdministratorResult> Handle(ConfirmAdministratorCommand request, CancellationToken cancellationToken)
    {
        if (request.Skip || request.StaffId is null || request.Pin is null)
            return ConfirmAdministratorResult.Success(new ConfirmAdministratorResponse(null));

        var verification = await verifyPin.VerifyAsync(request.LocationId, request.StaffId.Value, request.Pin, cancellationToken);
        if (!verification.Succeeded)
        {
            return verification.Failure switch
            {
                PinVerificationFailure.NotEligible => ConfirmAdministratorResult.NotEligible(),
                PinVerificationFailure.Invalid => ConfirmAdministratorResult.Invalid(verification.AttemptsRemaining!.Value),
                PinVerificationFailure.Locked => ConfirmAdministratorResult.Locked(verification.LockedUntil!.Value),
                _ => throw new InvalidOperationException($"Unhandled {nameof(PinVerificationFailure)}: {verification.Failure}"),
            };
        }

        // FR-017: a valid PIN alone is insufficient — the caregiver must actually be checked in.
        var isCheckedIn = await db.RoomShifts.AnyAsync(
            s => s.StaffProfileId == request.StaffId.Value && s.CheckedOutAt == null, cancellationToken);
        if (!isCheckedIn)
            return ConfirmAdministratorResult.NotCheckedIn();

        return ConfirmAdministratorResult.Success(new ConfirmAdministratorResponse(request.StaffId));
    }
}
