using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record ReactivateStaffProfileCommand(Guid Id) : IRequest<StaffResult>;

public class ReactivateStaffProfileCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<ReactivateStaffProfileCommand, StaffResult>
{
    public async Task<StaffResult> Handle(ReactivateStaffProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        if (profile.DeactivatedAt is not null)
        {
            profile.DeactivatedAt = null;
            profile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var user = await db.Users.FirstAsync(u => u.Id == profile.TenantUserId, cancellationToken);
        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == profile.Id)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);
        var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, eligibleLocationIds, photoUrl));
    }
}
