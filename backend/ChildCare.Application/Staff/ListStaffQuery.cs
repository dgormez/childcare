using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record ListStaffQuery(bool IncludeDeactivated = false) : IRequest<IReadOnlyList<StaffResponse>>;

public class ListStaffQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<ListStaffQuery, IReadOnlyList<StaffResponse>>
{
    public async Task<IReadOnlyList<StaffResponse>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        var query = db.StaffProfiles.AsQueryable();
        if (!request.IncludeDeactivated)
            query = query.Where(p => p.DeactivatedAt == null);

        var profiles = await query.ToListAsync(cancellationToken);
        if (profiles.Count == 0)
            return [];

        var profileIds = profiles.Select(p => p.Id).ToList();
        var userIds = profiles.Select(p => p.TenantUserId).ToList();

        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        var eligibility = await db.StaffLocationEligibility
            .Where(e => profileIds.Contains(e.StaffProfileId))
            .ToListAsync(cancellationToken);
        var eligibilityByProfile = eligibility
            .GroupBy(e => e.StaffProfileId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(e => e.LocationId).ToList());

        var results = new List<StaffResponse>(profiles.Count);
        foreach (var profile in profiles)
        {
            var user = users[profile.TenantUserId];
            var locations = eligibilityByProfile.TryGetValue(profile.Id, out var ids) ? ids : (IReadOnlyList<Guid>)[];
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);
            results.Add(StaffMapper.ToResponse(profile, user, locations, photoUrl));
        }

        return results;
    }
}
