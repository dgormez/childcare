using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using MediatR;

namespace ChildCare.Application.Locations;

public class CreateLocationCommandHandler(ITenantDbContext db) : IRequestHandler<CreateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = new Location
        {
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            MaxCapacity = request.MaxCapacity,
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
