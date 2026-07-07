using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

// GroupId/CallerRole/CallerTenantUserId are feature 008 additions (research.md R6): GroupId
// filters to children with a currently-active ChildGroupAssignment in that group; when
// CallerRole == "staff", results are further restricted to children with a currently-active
// assignment whose group's location is one CallerTenantUserId's StaffProfile is eligible for
// (resolved here, not by the endpoint, so this remains the one place the scoping rule lives) —
// a Director caller (or omitted parameters) sees unchanged feature-006 behavior.
public record ListChildrenQuery(
    bool IncludeDeactivated = false,
    Guid? GroupId = null,
    string? CallerRole = null,
    Guid? CallerTenantUserId = null) : IRequest<IReadOnlyList<ChildResponse>>;

public class ListChildrenQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<ListChildrenQuery, IReadOnlyList<ChildResponse>>
{
    public async Task<IReadOnlyList<ChildResponse>> Handle(ListChildrenQuery request, CancellationToken cancellationToken)
    {
        var query = db.Children.AsQueryable();
        if (!request.IncludeDeactivated)
            query = query.Where(c => c.DeactivatedAt == null);

        if (request.GroupId is Guid groupId)
        {
            var childIdsInGroup = db.ChildGroupAssignments
                .Where(a => a.GroupId == groupId && a.EndDate == null)
                .Select(a => a.ChildId);
            query = query.Where(c => childIdsInGroup.Contains(c.Id));
        }

        if (string.Equals(request.CallerRole, "staff", StringComparison.OrdinalIgnoreCase) && request.CallerTenantUserId is Guid tenantUserId)
        {
            var eligibleLocationIds = db.StaffProfiles
                .Where(p => p.TenantUserId == tenantUserId)
                .Join(db.StaffLocationEligibility, p => p.Id, e => e.StaffProfileId, (p, e) => e.LocationId);
            var childIdsAtEligibleLocations = db.ChildGroupAssignments
                .Where(a => a.EndDate == null)
                .Join(db.Groups, a => a.GroupId, g => g.Id, (a, g) => new { a.ChildId, g.LocationId })
                .Where(x => eligibleLocationIds.Contains(x.LocationId))
                .Select(x => x.ChildId);
            query = query.Where(c => childIdsAtEligibleLocations.Contains(c.Id));
        }

        var children = await query.ToListAsync(cancellationToken);

        var results = new List<ChildResponse>(children.Count);
        foreach (var child in children)
        {
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
            results.Add(ChildMapper.ToResponse(child, photoUrl));
        }

        return results;
    }
}
