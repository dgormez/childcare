using ChildCare.Application.Common;
using ChildCare.Application.MonthlyMenus;
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

        // FR-014 — lets the settings UI warn before a removal that would affect a real,
        // currently-visible-to-parents menu, without a separate round-trip.
        var menuVariantsWithPublishedContent = await MonthlyMenuVariantHelper.GetVariantsWithPublishedContentAsync(
            db, location.Id, location.MenuVariantPriorityOrder, cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location, menuVariantsWithPublishedContent));
    }
}
