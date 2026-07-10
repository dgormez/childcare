using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record ListBillableClosureDatesQuery(Guid LocationId, DateOnly From, DateOnly To) : IRequest<BillableClosureDatesResponse>;

public class ClosureCalendarReader(ITenantDbContext db)
    : IClosureCalendarReader, IRequestHandler<ListBillableClosureDatesQuery, BillableClosureDatesResponse>
{
    public async Task<IReadOnlyList<DateOnly>> ListPublishedClosureDatesAsync(
        Guid locationId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await db.KdvClosureDays
            .Where(c => c.LocationId == locationId
                     && c.Status == ClosureStatus.Published
                     && c.Date >= from
                     && c.Date <= to)
            .OrderBy(c => c.Date)
            .Select(c => c.Date)
            .ToListAsync(cancellationToken);

    public async Task<bool> IsPublishedClosureDateAsync(
        Guid locationId, DateOnly date, CancellationToken cancellationToken = default) =>
        await db.KdvClosureDays.AnyAsync(
            c => c.LocationId == locationId
              && c.Status == ClosureStatus.Published
              && c.Date == date,
            cancellationToken);

    public async Task<BillableClosureDatesResponse> Handle(ListBillableClosureDatesQuery request, CancellationToken cancellationToken)
    {
        var dates = await ListPublishedClosureDatesAsync(request.LocationId, request.From, request.To, cancellationToken);
        return new BillableClosureDatesResponse(request.LocationId, dates);
    }
}
