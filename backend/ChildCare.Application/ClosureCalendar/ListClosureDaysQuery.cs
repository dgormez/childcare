using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record ListClosureDaysQuery(Guid LocationId, int Year) : IRequest<ListClosureCalendarResult>;

public class ListClosureDaysQueryValidator : AbstractValidator<ListClosureDaysQuery>
{
    public ListClosureDaysQueryValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(1, 9999);
    }
}

public class ListClosureDaysQueryHandler(ITenantDbContext db) : IRequestHandler<ListClosureDaysQuery, ListClosureCalendarResult>
{
    public async Task<ListClosureCalendarResult> Handle(ListClosureDaysQuery request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return ListClosureCalendarResult.Fail(ClosureCalendarFailure.LocationNotFound);

        var from = new DateOnly(request.Year, 1, 1);
        var to = new DateOnly(request.Year, 12, 31);

        var closures = await db.KdvClosureDays
            .Where(c => c.LocationId == request.LocationId && c.Date >= from && c.Date <= to)
            .OrderBy(c => c.Date)
            .ToListAsync(cancellationToken);

        var ids = closures.Select(c => c.Id).ToList();
        var deliveries = await db.ClosureNotificationDeliveries
            .Where(d => ids.Contains(d.ClosureDayId))
            .ToListAsync(cancellationToken);

        return ListClosureCalendarResult.Success(closures.Select(c => ClosureCalendarMapper.ToResponse(
            c, deliveries.Where(d => d.ClosureDayId == c.Id).ToList())).ToList());
    }
}
