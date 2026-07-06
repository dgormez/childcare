using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// Last-write-wins (FR-017, research.md R3) — no concurrency token, whichever request's
/// SaveChangesAsync commits last simply overwrites the previous save's values.
/// </summary>
public class UpdateLocationCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        location.Name = request.Name;
        location.Address = request.Address;
        location.Phone = request.Phone;
        location.Email = request.Email;
        location.MaxCapacity = request.MaxCapacity;
        location.NaamLocatie = request.NaamLocatie;
        location.Dossiernummer = request.Dossiernummer;
        location.Verantwoordelijke = request.Verantwoordelijke;
        location.FlexPermission = request.FlexPermission;
        location.BoPermission = request.BoPermission;
        location.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
