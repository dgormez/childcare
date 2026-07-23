using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// Feature 027 deviation (flagged in the implementation report): staff-mobile's schedule screen
/// needs to resolve StaffSchedule.LocationId to a display name (spec.md UX Requirements — "each
/// entry shows location, group/room, start/end time"), but GET /api/locations is DirectorOnly
/// and its LocationResponse carries financial/PII fields (bank account, invoicing settings) a
/// staff member has no reason to see. This is a deliberately trimmed, name-only projection for
/// active locations, not a reuse of ListLocationsQuery's full response.
/// </summary>
public record ListStaffVisibleLocationsQuery : IRequest<IReadOnlyList<LocationNameResponse>>;

public record LocationNameResponse(Guid Id, string Name);

public class ListStaffVisibleLocationsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListStaffVisibleLocationsQuery, IReadOnlyList<LocationNameResponse>>
{
    public async Task<IReadOnlyList<LocationNameResponse>> Handle(ListStaffVisibleLocationsQuery request, CancellationToken cancellationToken)
    {
        return await db.Locations
            .Where(l => l.DeactivatedAt == null)
            .Select(l => new LocationNameResponse(l.Id, l.Name))
            .ToListAsync(cancellationToken);
    }
}
