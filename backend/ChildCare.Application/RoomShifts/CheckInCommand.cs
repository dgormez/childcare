using ChildCare.Application.Common;
using ChildCare.Application.Staff;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.RoomShifts;

/// <summary>
/// FR-009: select-then-PIN check-in (spec User Story 3). The client already identifies the
/// caregiver by tapping their photo card — this never searches for who a PIN belongs to
/// (VerifyPinCommand, research.md R6).
/// </summary>
public record CheckInCommand(Guid DeviceId, Guid LocationId, Guid GroupId, Guid StaffId, string? Pin) : IRequest<CheckInResult>;

/// <summary>
/// Feature 008b: when the location's RequiresCaregiverPin is false, the tapped StaffId alone
/// completes check-in — VerifyPinCommand.VerifyAsync is still called (it still enforces the
/// caregiver exists, isn't deactivated, and is eligible at this location, per spec.md
/// Assumptions), but with a null pin so it skips the bcrypt compare and lockout bookkeeping
/// entirely (FR-004/FR-007). When the setting is true (the default), a null/empty client pin is
/// coerced to empty string, never passed through as null — null must only ever mean "the server
/// decided verification doesn't apply here", never "the client happened to omit a pin", or a
/// PIN-required location could be bypassed simply by sending no pin.
/// </summary>
public class CheckInCommandHandler(VerifyPinCommand verifyPin, CloseStaleShiftsHelper closeStaleShifts, ITenantDbContext db)
    : IRequestHandler<CheckInCommand, CheckInResult>
{
    public async Task<CheckInResult> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        await closeStaleShifts.CloseStaleShiftsAsync(request.LocationId, DateTime.UtcNow, cancellationToken);

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        var pinToVerify = location is { RequiresCaregiverPin: false } ? null : request.Pin ?? string.Empty;

        var verification = await verifyPin.VerifyAsync(request.LocationId, request.StaffId, pinToVerify, cancellationToken);
        if (!verification.Succeeded)
        {
            return verification.Failure switch
            {
                PinVerificationFailure.NotEligible => CheckInResult.NotEligible(),
                PinVerificationFailure.Invalid => CheckInResult.Invalid(verification.AttemptsRemaining!.Value),
                PinVerificationFailure.Locked => CheckInResult.Locked(verification.LockedUntil!.Value),
                _ => throw new InvalidOperationException($"Unhandled {nameof(PinVerificationFailure)}: {verification.Failure}"),
            };
        }

        var openShifts = await db.RoomShifts
            .Where(s => s.StaffProfileId == request.StaffId && s.CheckedOutAt == null)
            .ToListAsync(cancellationToken);

        // Stale-card protection (contracts/room-shift-api.md) — a caregiver only ever taps a
        // not-checked-in card to reach check-in, so an open shift already at *this* location
        // means the client's roster is out of date.
        if (openShifts.Any(s => s.LocationId == request.LocationId))
            return CheckInResult.AlreadyCheckedIn();

        var now = DateTime.UtcNow;

        // data-model.md: a caregiver can't have two open shifts across different rooms — any
        // shift still open elsewhere is treated as an implicit reassignment and auto-closed.
        foreach (var other in openShifts)
        {
            other.CheckedOutAt = now;
            other.ClosedReason = "reassigned";
        }

        db.RoomShifts.Add(new RoomShift
        {
            StaffProfileId = request.StaffId,
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            DevicePairingId = request.DeviceId,
            CheckedInAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        var profile = verification.StaffProfile!;
        return CheckInResult.Success(new CheckInResponse(profile.Id, profile.FirstName, now));
    }
}
