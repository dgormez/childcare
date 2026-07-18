using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

/// <summary>
/// FR-001/FR-002/FR-003: today's actual occupancy (per group and per location) plus a week-ahead
/// projection per location. Today is computed from live `AttendanceRecord` data (research.md R1)
/// — attendance doesn't exist yet for future dates, so the week-ahead figures reuse
/// `WaitingList.GetOccupancyQuery`'s existing contract-based projection unmodified.
/// </summary>
public record GetOccupancySummaryQuery(Guid? LocationId) : IRequest<OccupancySummaryResponse>;

public class GetOccupancySummaryQueryHandler(ITenantDbContext db, IMediator mediator)
    : IRequestHandler<GetOccupancySummaryQuery, OccupancySummaryResponse>
{
    public async Task<OccupancySummaryResponse> Handle(GetOccupancySummaryQuery request, CancellationToken cancellationToken)
    {
        var today = BelgianCalendarDay.Today();

        var locationsQuery = db.Locations.Where(l => l.DeactivatedAt == null);
        if (request.LocationId is not null)
            locationsQuery = locationsQuery.Where(l => l.Id == request.LocationId);
        var locations = await locationsQuery.ToListAsync(cancellationToken);

        var responses = new List<OccupancyLocationSummaryResponse>();
        foreach (var location in locations)
        {
            var presentChildIds = await db.AttendanceRecords
                .Where(r => r.LocationId == location.Id && r.Date == today
                            && r.Status == AttendanceStatus.Present && r.CheckOutAt == null)
                .Select(r => r.ChildId)
                .ToListAsync(cancellationToken);

            var groups = await db.Groups.Where(g => g.LocationId == location.Id).ToListAsync(cancellationToken);

            var assignments = groups.Count == 0
                ? []
                : await db.ChildGroupAssignments
                    .Where(a => presentChildIds.Contains(a.ChildId) && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
                    .ToListAsync(cancellationToken);

            var groupPresentCounts = assignments
                .GroupBy(a => a.GroupId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.ChildId).Distinct().Count());

            var groupResponses = groups.Select(g =>
            {
                var presentCount = groupPresentCounts.GetValueOrDefault(g.Id, 0);
                return new OccupancyGroupSummaryResponse(
                    g.Id, g.Name, presentCount, g.Capacity,
                    ReportingMapper.ComputeOccupancyStatus(presentCount, g.Capacity));
            }).ToList();

            var weekAheadResult = await mediator.Send(
                new ChildCare.Application.WaitingList.GetOccupancyQuery(location.Id, today, today.AddDays(6)),
                cancellationToken);
            var weekAhead = weekAheadResult.Succeeded ? weekAheadResult.Days : [];

            responses.Add(new OccupancyLocationSummaryResponse(
                location.Id,
                location.Name,
                presentChildIds.Count,
                location.MaxCapacity,
                ReportingMapper.ComputeOccupancyStatus(presentChildIds.Count, location.MaxCapacity)!,
                groupResponses,
                weekAhead));
        }

        return new OccupancySummaryResponse(today, responses);
    }
}
