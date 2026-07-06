using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record AssignLocationEligibilityCommand(Guid StaffProfileId, Guid LocationId) : IRequest<StaffResult>;

public class AssignLocationEligibilityCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<AssignLocationEligibilityCommand, StaffResult>
{
    public async Task<StaffResult> Handle(AssignLocationEligibilityCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId, cancellationToken);
        if (!locationExists)
            return StaffResult.Fail(StaffFailure.LocationNotFound);

        var alreadyAssigned = await db.StaffLocationEligibility.AnyAsync(
            e => e.StaffProfileId == request.StaffProfileId && e.LocationId == request.LocationId, cancellationToken);

        if (!alreadyAssigned)
        {
            db.StaffLocationEligibility.Add(new StaffLocationEligibility
            {
                StaffProfileId = request.StaffProfileId,
                LocationId = request.LocationId,
            });
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
