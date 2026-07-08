using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

/// <summary>
/// FR-006/FR-007: director sets/resets a caregiver's 4-digit check-in PIN. Enforces uniqueness
/// within a location (data-model.md's "Uniqueness rule") — since PINs are salted bcrypt
/// hashes, this is done by bcrypt-comparing the *new* PIN against every other candidate
/// caregiver's existing hash, not a direct query.
/// </summary>
public record SetCaregiverPinCommand(Guid StaffProfileId, string Pin) : IRequest<PinManagementResult>;

public class SetCaregiverPinCommandHandler(ITenantDbContext db) : IRequestHandler<SetCaregiverPinCommand, PinManagementResult>
{
    public async Task<PinManagementResult> Handle(SetCaregiverPinCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return PinManagementResult.Fail(PinManagementFailure.NotFound);

        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == request.StaffProfileId)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);

        if (eligibleLocationIds.Count > 0)
        {
            var candidateStaffIds = await db.StaffLocationEligibility
                .Where(e => eligibleLocationIds.Contains(e.LocationId) && e.StaffProfileId != request.StaffProfileId)
                .Select(e => e.StaffProfileId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var candidateHashes = await db.StaffProfiles
                .Where(p => candidateStaffIds.Contains(p.Id) && p.PinHash != null)
                .Select(p => p.PinHash!)
                .ToListAsync(cancellationToken);

            if (candidateHashes.Any(hash => BCrypt.Net.BCrypt.Verify(request.Pin, hash)))
                return PinManagementResult.Fail(PinManagementFailure.NotUniqueAtLocation);
        }

        profile.PinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
        profile.PinFailedAttempts = 0;
        profile.PinFirstFailedAttemptAt = null;
        profile.PinLockedUntil = null;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return PinManagementResult.Success();
    }
}
