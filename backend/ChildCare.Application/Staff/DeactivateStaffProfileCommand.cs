using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record DeactivateStaffProfileCommand(Guid Id) : IRequest<StaffResult>;

/// <summary>
/// FR-010/SC-004: deactivating a `Staff`-role account blocks its next login and invalidates all
/// of its active refresh tokens (mirrors ResetPasswordCommandHandler's session-invalidation,
/// feature 003) — an already-issued 15-minute access token is not proactively revoked (no
/// revocation list exists anywhere in this codebase), but no new one can be silently obtained.
/// A `Director`-role account's own Staff Profile has no such side effect — see
/// LoginCommandHandler's Role == Staff condition.
/// </summary>
public class DeactivateStaffProfileCommandHandler(ITenantDbContext db, IEnumerable<IStaffDeactivationGuard> guards, IProfilePhotoStorage photoStorage)
    : IRequestHandler<DeactivateStaffProfileCommand, StaffResult>
{
    public async Task<StaffResult> Handle(DeactivateStaffProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        foreach (var guard in guards)
        {
            if (await guard.HasActiveDependentsAsync(request.Id, db, cancellationToken))
                return StaffResult.Fail(StaffFailure.HasActiveDependents);
        }

        var user = await db.Users.FirstAsync(u => u.Id == profile.TenantUserId, cancellationToken);

        if (profile.DeactivatedAt is null)
        {
            profile.DeactivatedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            if (user.Role == UserRole.Staff)
            {
                var tokens = db.RefreshTokens.Where(t => t.TenantUserId == user.Id);
                db.RefreshTokens.RemoveRange(tokens);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == profile.Id)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);
        var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, eligibleLocationIds, photoUrl));
    }
}
