using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public class UpdateStaffProfileCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<UpdateStaffProfileCommand, StaffResult>
{
    public async Task<StaffResult> Handle(UpdateStaffProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.Phone = request.Phone;
        profile.QualificationLevel = request.QualificationLevel;
        if (request.ContractedDays is not null)
            profile.ContractedDays = request.ContractedDays.Distinct().ToList();
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var user = await db.Users.FirstAsync(u => u.Id == profile.TenantUserId, cancellationToken);
        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == profile.Id)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);
        var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, eligibleLocationIds, photoUrl));
    }
}
