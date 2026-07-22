using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

/// <summary>
/// Feature 027 deviation (flagged in the implementation report, same class of gap as
/// ListStaffVisibleLocationsQuery): staff-mobile's schedule screen needs to show a closure day
/// as "KDV gesloten" (FR-004), but GET /api/closures is DirectorOnly and its response carries
/// parent-notification delivery details a staff member has no reason to see. This is a
/// deliberately trimmed (LocationId, Date) projection of Published closures within a bounded
/// range, across every active location — matches the 4-week horizon spec.md's UX Requirements
/// describe, not a per-location year-at-a-time read like the director grid's own query.
/// </summary>
public record ListStaffVisibleClosureDatesQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<StaffVisibleClosureDateResponse>>;

public record StaffVisibleClosureDateResponse(Guid LocationId, DateOnly Date);

public class ListStaffVisibleClosureDatesQueryValidator : AbstractValidator<ListStaffVisibleClosureDatesQuery>
{
    public ListStaffVisibleClosureDatesQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
    }
}

public class ListStaffVisibleClosureDatesQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListStaffVisibleClosureDatesQuery, IReadOnlyList<StaffVisibleClosureDateResponse>>
{
    public async Task<IReadOnlyList<StaffVisibleClosureDateResponse>> Handle(ListStaffVisibleClosureDatesQuery request, CancellationToken cancellationToken)
    {
        return await db.KdvClosureDays
            .Where(c => c.Date >= request.From && c.Date <= request.To && c.Status == ClosureStatus.Published)
            .Select(c => new StaffVisibleClosureDateResponse(c.LocationId, c.Date))
            .ToListAsync(cancellationToken);
    }
}
