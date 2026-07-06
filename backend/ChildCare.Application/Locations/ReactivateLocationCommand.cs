using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

public record ReactivateLocationCommand(Guid Id) : IRequest<LocationResult>;

public class ReactivateLocationCommandHandler(ITenantDbContext db) : IRequestHandler<ReactivateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(ReactivateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        // Idempotent — reactivating an already-active location is a no-op success.
        if (location.DeactivatedAt is not null)
        {
            location.DeactivatedAt = null;
            location.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
