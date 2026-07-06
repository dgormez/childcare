using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

public record GetLocationByIdQuery(Guid Id) : IRequest<LocationResult>;

public class GetLocationByIdQueryHandler(ITenantDbContext db) : IRequestHandler<GetLocationByIdQuery, LocationResult>
{
    public async Task<LocationResult> Handle(GetLocationByIdQuery request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
