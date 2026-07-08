using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Devices;

/// <summary>
/// FR-005: director exits room mode via the device's own override PIN — a single-target bcrypt
/// comparison against DirectorOverridePinHash, so unlike VerifyPinCommand's per-caregiver
/// design this tracks its own lockout directly on the DevicePairing row (contracts/
/// device-pairing-api.md's explicit exception; not part of the shared caregiver-PIN lockout).
/// On success, also closes any RoomShift still open under this DevicePairingId
/// (ClosedReason = "reassigned") — the natural place to do this, since re-pairing always goes
/// through an exit-room-mode call first, and this call is DeviceAuthenticated so it already
/// knows exactly which DevicePairing is exiting, with no separate hardware-id lookup needed
/// (FR-026, research.md).
/// </summary>
public record ExitRoomModeCommand(Guid DeviceId, string OverridePin) : IRequest<ExitRoomModeResult>;

public class ExitRoomModeCommandHandler(ITenantDbContext db) : IRequestHandler<ExitRoomModeCommand, ExitRoomModeResult>
{
    // Deliberately more lenient than caregiver-PIN lockout (5/2min/10min) — a 6-digit PIN has
    // far higher entropy and is used far less often, by a director, not guessed at by a
    // curious child (spec FR-005).
    private const int MaxAttempts = 10;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    public async Task<ExitRoomModeResult> Handle(ExitRoomModeCommand request, CancellationToken cancellationToken)
    {
        var pairing = await db.DevicePairings.FirstAsync(d => d.Id == request.DeviceId, cancellationToken);

        var now = DateTime.UtcNow;
        if (pairing.OverridePinLockedUntil is { } lockedUntil && lockedUntil > now)
            return ExitRoomModeResult.Locked(lockedUntil);

        var isMatch = BCrypt.Net.BCrypt.Verify(request.OverridePin, pairing.DirectorOverridePinHash);

        if (isMatch)
        {
            pairing.OverridePinFailedAttempts = 0;
            pairing.OverridePinFirstFailedAttemptAt = null;
            pairing.OverridePinLockedUntil = null;

            var openShifts = await db.RoomShifts
                .Where(s => s.DevicePairingId == pairing.Id && s.CheckedOutAt == null)
                .ToListAsync(cancellationToken);
            foreach (var shift in openShifts)
            {
                shift.CheckedOutAt = now;
                shift.ClosedReason = "reassigned";
            }

            await db.SaveChangesAsync(cancellationToken);
            return ExitRoomModeResult.Success();
        }

        // Sliding window anchored to the first failure in the current streak (spec FR-005,
        // same shape as VerifyPinCommand's caregiver-PIN lockout, different thresholds).
        if (pairing.OverridePinFirstFailedAttemptAt is null || now - pairing.OverridePinFirstFailedAttemptAt > LockoutWindow)
        {
            pairing.OverridePinFailedAttempts = 1;
            pairing.OverridePinFirstFailedAttemptAt = now;
        }
        else
        {
            pairing.OverridePinFailedAttempts++;
        }

        DateTime? newLockedUntil = null;
        if (pairing.OverridePinFailedAttempts >= MaxAttempts)
        {
            newLockedUntil = now.Add(LockoutDuration);
            pairing.OverridePinLockedUntil = newLockedUntil;
        }

        await db.SaveChangesAsync(cancellationToken);

        return newLockedUntil is { } lockedAt
            ? ExitRoomModeResult.Locked(lockedAt)
            : ExitRoomModeResult.Invalid(MaxAttempts - pairing.OverridePinFailedAttempts);
    }
}
