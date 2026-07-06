using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

public record DuplicateLocationCommand(Guid SourceLocationId) : IRequest<LocationResult>;

/// <summary>
/// Create-time convenience only — no persisted link between source and copy (FR-015,
/// research.md R5). The new location is a fully independent record from the moment it exists.
/// </summary>
public class DuplicateLocationCommandHandler(ITenantDbContext db) : IRequestHandler<DuplicateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(DuplicateLocationCommand request, CancellationToken cancellationToken)
    {
        var source = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.SourceLocationId, cancellationToken);
        if (source is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        var copy = new Location
        {
            Name = source.Name,
            Address = source.Address,
            Phone = source.Phone,
            Email = source.Email,
            MaxCapacity = source.MaxCapacity,
            NaamLocatie = source.NaamLocatie,
            Dossiernummer = source.Dossiernummer,
            Verantwoordelijke = source.Verantwoordelijke,
            FlexPermission = source.FlexPermission,
            BoPermission = source.BoPermission,
        };

        db.Locations.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(copy));
    }
}
