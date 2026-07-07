using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

// CallerRole/CallerTenantUserId are feature 008 additions (research.md R6): when
// CallerRole == "staff", results are restricted to locations that CallerTenantUserId's
// StaffProfile is eligible for (resolved here, not by the endpoint), regardless of whether
// LocationId is also supplied — a Director caller (or omitted parameters) sees unchanged
// feature-006 behavior.
public record ListGroupsQuery(
    Guid? LocationId = null,
    string? CallerRole = null,
    Guid? CallerTenantUserId = null) : IRequest<IReadOnlyList<GroupResponse>>;

public class ListGroupsQueryHandler(ITenantDbContext db) : IRequestHandler<ListGroupsQuery, IReadOnlyList<GroupResponse>>
{
    public async Task<IReadOnlyList<GroupResponse>> Handle(ListGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Groups.AsQueryable();
        if (request.LocationId is Guid locationId)
            query = query.Where(g => g.LocationId == locationId);

        if (string.Equals(request.CallerRole, "staff", StringComparison.OrdinalIgnoreCase) && request.CallerTenantUserId is Guid tenantUserId)
        {
            var eligibleLocationIds = db.StaffProfiles
                .Where(p => p.TenantUserId == tenantUserId)
                .Join(db.StaffLocationEligibility, p => p.Id, e => e.StaffProfileId, (p, e) => e.LocationId);
            query = query.Where(g => eligibleLocationIds.Contains(g.LocationId));
        }

        var groups = await query.ToListAsync(cancellationToken);
        return groups.Select(GroupMapper.ToResponse).ToList();
    }
}
