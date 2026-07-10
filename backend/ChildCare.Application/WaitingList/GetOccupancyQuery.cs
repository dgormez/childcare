using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

/// <summary>
/// FR-013/FR-014/FR-015. Forward-looking occupancy projected from active Contracts and
/// Location.MaxCapacity — never from AttendanceRecord (research.md R1, feature 010's data is
/// same-day/historical and doesn't exist for future dates).
/// </summary>
public record GetOccupancyQuery(Guid LocationId, DateOnly From, DateOnly To) : IRequest<OccupancyResult>;

public class GetOccupancyQueryValidator : AbstractValidator<GetOccupancyQuery>
{
    public GetOccupancyQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 366)
            .WithMessage("errors.validation");
    }
}

public class GetOccupancyQueryHandler(ITenantDbContext db) : IRequestHandler<GetOccupancyQuery, OccupancyResult>
{
    public async Task<OccupancyResult> Handle(GetOccupancyQuery request, CancellationToken cancellationToken)
    {
        // spec.md Edge Cases: a deactivated location's existing entries stay visible on the
        // list, but occupancy cannot be projected for it — matches
        // CreateWaitingListEntryCommand's existing DeactivatedAt precedent.
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (location is null)
            return OccupancyResult.Fail(WaitingListFailure.LocationNotFound);

        var closedDates = await db.KdvClosureDays
            .Where(c => c.LocationId == request.LocationId && c.Status == ClosureStatus.Published && c.Date >= request.From && c.Date <= request.To)
            .Select(c => c.Date)
            .ToListAsync(cancellationToken);
        var closedSet = closedDates.ToHashSet();

        // Active contracts whose date range could possibly intersect [From, To] — filtered
        // further per-date below (weekday coverage can't be expressed in SQL simply for a
        // JSONB-owned collection, so the day loop below evaluates it in memory).
        var candidateContracts = await db.Contracts
            .AsNoTracking()
            .Where(c => c.LocationId == request.LocationId
                && c.Status == Domain.Enums.ContractStatus.Active
                && c.StartDate <= request.To
                && (c.EndDate == null || c.EndDate >= request.From))
            .Select(c => new { c.StartDate, c.EndDate, c.ContractedDays })
            .ToListAsync(cancellationToken);

        var days = new List<OccupancyDayResponse>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            if (closedSet.Contains(date))
            {
                days.Add(new OccupancyDayResponse(date, FreeCapacity: null, Closed: true));
                continue;
            }

            var occupiedCount = candidateContracts.Count(c =>
                c.StartDate <= date &&
                (c.EndDate == null || c.EndDate >= date) &&
                c.ContractedDays.Any(cd => cd.Weekday == date.DayOfWeek));

            days.Add(new OccupancyDayResponse(date, location.MaxCapacity - occupiedCount, Closed: false));
        }

        return OccupancyResult.Success(days);
    }
}
