using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

/// <summary>
/// The single shared PIN-check path (research.md R2/R6) used by check-in, check-out, and
/// sensitive-action confirmation alike (spec Clarifications). Select-then-PIN (spec User Story
/// 3) means every caller already knows which caregiver is being verified — this never searches
/// a candidate set, it loads exactly one StaffProfile by id and confirms it's eligible at the
/// device's own location before touching the PIN at all (FR-004/024/025 fall out of that one
/// check). Not a MediatR command — a plain injectable service called from within CheckInCommand/
/// CheckOutCommand/ConfirmAdministratorCommand's own handlers, mirroring IShiftAttributionService/
/// CloseStaleShiftsHelper's established pattern for shared cross-command logic.
///
/// Feature 008b: <paramref name="pin"/> is nullable. A null pin means the caller (CheckInCommand/
/// CheckOutCommand, per the location's RequiresCaregiverPin setting) has already decided PIN
/// verification doesn't apply — the profile-exists/not-deactivated/location-eligible checks below
/// still run (the tap-to-identify claim must still name a real, eligible caregiver, spec.md
/// Assumptions), but the bcrypt compare and PIN lockout bookkeeping are skipped entirely, since
/// there is nothing to lock someone out of when no PIN was ever checked.
/// </summary>
public class VerifyPinCommand(ITenantDbContext db)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    public async Task<PinVerificationResult> VerifyAsync(
        Guid locationId, Guid staffId, string? pin, CancellationToken cancellationToken = default)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == staffId, cancellationToken);
        if (profile is null || profile.DeactivatedAt is not null)
            return PinVerificationResult.NotEligible();

        var eligible = await db.StaffLocationEligibility.AnyAsync(
            e => e.StaffProfileId == staffId && e.LocationId == locationId, cancellationToken);
        if (!eligible)
            return PinVerificationResult.NotEligible();

        if (pin is null)
            return PinVerificationResult.Success(profile);

        var now = DateTime.UtcNow;
        if (profile.PinLockedUntil is { } lockedUntil && lockedUntil > now)
            return PinVerificationResult.Locked(lockedUntil);

        var isMatch = !string.IsNullOrEmpty(profile.PinHash) && BCrypt.Net.BCrypt.Verify(pin, profile.PinHash);

        if (isMatch)
        {
            profile.PinFailedAttempts = 0;
            profile.PinFirstFailedAttemptAt = null;
            profile.PinLockedUntil = null;
            profile.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return PinVerificationResult.Success(profile);
        }

        // Sliding window anchored to the *first* failure in the current streak, not a fixed
        // clock-aligned window (data-model.md, spec FR-012) — prevents an attacker pacing
        // exactly 4 attempts per fixed window indefinitely.
        if (profile.PinFirstFailedAttemptAt is null || now - profile.PinFirstFailedAttemptAt > LockoutWindow)
        {
            profile.PinFailedAttempts = 1;
            profile.PinFirstFailedAttemptAt = now;
        }
        else
        {
            profile.PinFailedAttempts++;
        }

        DateTime? newLockedUntil = null;
        if (profile.PinFailedAttempts >= MaxAttempts)
        {
            newLockedUntil = now.Add(LockoutDuration);
            profile.PinLockedUntil = newLockedUntil;
        }

        profile.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        // The request that *triggers* the lockout gets the Locked response too, not Invalid —
        // never conflate the two shapes (spec FR-012, CHK008).
        return newLockedUntil is { } lockedAt
            ? PinVerificationResult.Locked(lockedAt)
            : PinVerificationResult.Invalid(MaxAttempts - profile.PinFailedAttempts);
    }
}
