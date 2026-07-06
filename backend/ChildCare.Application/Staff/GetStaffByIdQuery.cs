using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record GetStaffByIdQuery(Guid Id) : IRequest<StaffResult>;

public class GetStaffByIdQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<GetStaffByIdQuery, StaffResult>
{
    public async Task<StaffResult> Handle(GetStaffByIdQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == profile.TenantUserId, cancellationToken);
        if (user is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == profile.Id)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, eligibleLocationIds, photoUrl));
    }
}
