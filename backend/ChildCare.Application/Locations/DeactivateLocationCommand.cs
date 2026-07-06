using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

public record DeactivateLocationCommand(Guid Id) : IRequest<LocationResult>;

public class DeactivateLocationCommandHandler(ITenantDbContext db, IEnumerable<ILocationDeactivationGuard> guards)
    : IRequestHandler<DeactivateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(DeactivateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        foreach (var guard in guards)
        {
            if (await guard.HasActiveDependentsAsync(request.Id, db, cancellationToken))
                return LocationResult.Fail(LocationFailure.HasActiveDependents);
        }

        // Idempotent — deactivating an already-deactivated location is a no-op success.
        if (location.DeactivatedAt is null)
        {
            location.DeactivatedAt = DateTime.UtcNow;
            location.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
