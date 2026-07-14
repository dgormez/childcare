using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Current-month default resolved at the endpoint layer (mirrors GetParentGroupActivityGalleryQuery's
// pattern, feature 009b). One entry per (location, child) pair — feature 013j restructured this
// from one entry per location (013e/research.md R4/R5), since variant resolution is inherently
// per-child: a parent's children at the same location can resolve to different menus depending
// on their individual MealPreference.DietaryType. A child holding contracts at two locations
// simultaneously (constitution Principle II's split-location scenario) resolves independently at
// each — see specs/013j-monthly-menu-variants/data-model.md's resolution algorithm.
public record GetParentMonthlyMenuQuery(Guid TenantUserId, int Year, int Month) : IRequest<GetParentMonthlyMenuResult>;

public class GetParentMonthlyMenuResult
{
    public bool Authorized { get; private init; }
    public List<ParentMonthlyMenuEntry>? Entries { get; private init; }

    public static GetParentMonthlyMenuResult Ok(List<ParentMonthlyMenuEntry> entries) => new() { Authorized = true, Entries = entries };
    public static GetParentMonthlyMenuResult Forbidden() => new() { Authorized = false };
}

public class GetParentMonthlyMenuQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IClosureCalendarReader closureCalendar) : IRequestHandler<GetParentMonthlyMenuQuery, GetParentMonthlyMenuResult>
{
    public async Task<GetParentMonthlyMenuResult> Handle(GetParentMonthlyMenuQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return GetParentMonthlyMenuResult.Forbidden();

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        if (childIds.Count == 0)
            return GetParentMonthlyMenuResult.Ok([]);

        // (location, child) pairs from every active contract — a child can hold contracts at more
        // than one location simultaneously; each pair resolves independently below.
        var locationChildPairs = await db.Contracts
            .Where(c => childIds.Contains(c.ChildId) && c.Status == ContractStatus.Active)
            .Select(c => new { c.LocationId, c.ChildId })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (locationChildPairs.Count == 0)
            return GetParentMonthlyMenuResult.Ok([]);

        var relevantLocationIds = locationChildPairs.Select(p => p.LocationId).Distinct().ToList();
        var locations = await db.Locations
            .Where(l => relevantLocationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var relevantChildIds = locationChildPairs.Select(p => p.ChildId).Distinct().ToList();
        var children = await db.Children
            .Where(c => relevantChildIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        var preferences = await db.MealPreferences
            .Where(mp => relevantChildIds.Contains(mp.ChildId))
            .ToDictionaryAsync(mp => mp.ChildId, cancellationToken);

        var monthStart = new DateOnly(request.Year, request.Month, 1);
        var monthEndInclusive = monthStart.AddMonths(1).AddDays(-1);

        var entries = new List<ParentMonthlyMenuEntry>();

        foreach (var locationId in relevantLocationIds)
        {
            var location = locations[locationId];

            // One query per location, not per child (research.md's efficiency decision) — every
            // published menu (base + enabled variants) for this location/month, keyed by variant
            // (null = base).
            var publishedMenus = await db.MonthlyMenus
                .Include(m => m.Days)
                .Where(m => m.LocationId == locationId && m.Year == request.Year && m.Month == request.Month && m.PublishedAt != null)
                .ToListAsync(cancellationToken);
            var publishedByVariant = publishedMenus.ToDictionary(m => m.Variant, m => m);

            var closureDates = await closureCalendar.ListPublishedClosureDatesAsync(locationId, monthStart, monthEndInclusive, cancellationToken);

            foreach (var childId in locationChildPairs.Where(p => p.LocationId == locationId).Select(p => p.ChildId).Distinct())
            {
                var child = children[childId];
                var childDietaryTypes = preferences.TryGetValue(childId, out var pref) ? pref.DietaryType : [];

                // FR-008: exact DietaryType equality only, no inferred hierarchy between types.
                DietaryType? resolvedVariant = null;
                foreach (var candidate in location.MenuVariantPriorityOrder)
                {
                    if (childDietaryTypes.Contains(candidate) && publishedByVariant.ContainsKey(candidate))
                    {
                        resolvedVariant = candidate;
                        break;
                    }
                }

                // FR-009: falls back to the base menu (key null) when nothing matched above.
                var resolvedMenu = publishedByVariant.GetValueOrDefault(resolvedVariant);

                entries.Add(new ParentMonthlyMenuEntry(
                    LocationId: locationId,
                    LocationName: location.Name,
                    ChildId: childId,
                    ChildName: child.FirstName,
                    ResolvedVariant: resolvedVariant?.ToWireString(),
                    IsPublished: resolvedMenu is not null,
                    Days: resolvedMenu is null ? [] : resolvedMenu.Days.OrderBy(d => d.MenuDate).Select(MonthlyMenuMapper.ToDayEntry).ToList(),
                    ClosureDates: closureDates));
            }
        }

        return GetParentMonthlyMenuResult.Ok(entries);
    }
}
