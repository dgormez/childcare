using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

public record ListLocationsQuery(bool IncludeDeactivated = false) : IRequest<IReadOnlyList<LocationResponse>>;

public class ListLocationsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListLocationsQuery, IReadOnlyList<LocationResponse>>
{
    public async Task<IReadOnlyList<LocationResponse>> Handle(ListLocationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Locations.AsQueryable();
        if (!request.IncludeDeactivated)
            query = query.Where(l => l.DeactivatedAt == null);

        var locations = await query.ToListAsync(cancellationToken);
        return locations.Select(LocationMapper.ToResponse).ToList();
    }
}
