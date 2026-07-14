using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Current-month default resolved at the endpoint layer (mirrors GetParentGroupActivityGalleryQuery's
// pattern, feature 009b). One entry per distinct location where any of the parent's linked children
// holds an active contract (research.md R5) — not just one "primary" location.
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

        var locations = await db.Contracts
            .Where(c => childIds.Contains(c.ChildId) && c.Status == ContractStatus.Active)
            .Select(c => c.LocationId)
            .Distinct()
            .Join(db.Locations, id => id, l => l.Id, (id, l) => new { l.Id, l.Name })
            .ToListAsync(cancellationToken);

        var monthStart = new DateOnly(request.Year, request.Month, 1);
        var monthEndInclusive = monthStart.AddMonths(1).AddDays(-1);

        var entries = new List<ParentMonthlyMenuEntry>();
        foreach (var location in locations)
        {
            var menu = await db.MonthlyMenus
                .Include(m => m.Days)
                .FirstOrDefaultAsync(
                    m => m.LocationId == location.Id && m.Year == request.Year && m.Month == request.Month && m.PublishedAt != null,
                    cancellationToken);

            var closureDates = await closureCalendar.ListPublishedClosureDatesAsync(location.Id, monthStart, monthEndInclusive, cancellationToken);

            entries.Add(new ParentMonthlyMenuEntry(
                LocationId: location.Id,
                LocationName: location.Name,
                IsPublished: menu is not null,
                Days: menu is null ? [] : menu.Days.OrderBy(d => d.MenuDate).Select(MonthlyMenuMapper.ToDayEntry).ToList(),
                ClosureDates: closureDates));
        }

        return GetParentMonthlyMenuResult.Ok(entries);
    }
}
