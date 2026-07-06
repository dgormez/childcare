using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record UnassignLocationEligibilityCommand(Guid StaffProfileId, Guid LocationId) : IRequest<StaffResult>;

public class UnassignLocationEligibilityCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<UnassignLocationEligibilityCommand, StaffResult>
{
    public async Task<StaffResult> Handle(UnassignLocationEligibilityCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        var existing = await db.StaffLocationEligibility.FirstOrDefaultAsync(
            e => e.StaffProfileId == request.StaffProfileId && e.LocationId == request.LocationId, cancellationToken);

        if (existing is not null)
        {
            db.StaffLocationEligibility.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        var user = await db.Users.FirstAsync(u => u.Id == profile.TenantUserId, cancellationToken);
        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == request.StaffProfileId)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);
        var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, eligibleLocationIds, photoUrl));
    }
}
