using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

/// <summary>
/// FR-005: on-demand reconstruction of BKR breach windows per group over a date range (default
/// last 30 days), per research.md R3 — no persisted breach-event log. Each calendar day is
/// reconstructed independently starting from zero present/staff: `AttendanceRecord` is always
/// per-day (unique per child/location/day) and `RoomShift`s are auto-closed at the local
/// midnight boundary (`CloseStaleShiftsHelper`), so a day never carries state over from the
/// previous one. Nap-time's threshold widening (research.md's `GetBkrRatioQuery` precedent) is
/// intentionally NOT reconstructed here — determining historical nap state at each instant would
/// require tracking sleep-event windows per timestamp, adding significant complexity for a
/// breach-*history* view; using the stricter non-nap threshold instead only ever over-flags a
/// possible breach for director review, never under-flags one, which is the safer default for a
/// compliance-adjacent report.
/// </summary>
public record GetBkrBreachHistoryQuery(Guid? LocationId, DateOnly? From, DateOnly? To) : IRequest<BkrBreachHistoryResponse>;

/// <summary>Mirrors GetOccupancyQueryValidator's range-validation precedent (feature 012a) —
/// a violation throws ValidationException via the shared pipeline behavior and surfaces as
/// 422 (errors.validation), the established convention for every FluentValidation failure in
/// this codebase (constitution Principle III).</summary>
public class GetBkrBreachHistoryQueryValidator : AbstractValidator<GetBkrBreachHistoryQuery>
{
    public GetBkrBreachHistoryQueryValidator()
    {
        RuleFor(x => x).Must(x => x.To is null || x.From is null || x.To >= x.From).WithMessage("errors.validation");
        RuleFor(x => x).Must(x => x.To is null || x.From is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366).WithMessage("errors.validation");
    }
}

public class GetBkrBreachHistoryQueryHandler(ITenantDbContext db)
    : IRequestHandler<GetBkrBreachHistoryQuery, BkrBreachHistoryResponse>
{
    private record TimestampedDelta(DateTime At, int PresentDelta, int StaffDelta);

    public async Task<BkrBreachHistoryResponse> Handle(GetBkrBreachHistoryQuery request, CancellationToken cancellationToken)
    {
        var today = BelgianCalendarDay.Today();
        var to = request.To ?? today;
        var from = request.From ?? to.AddDays(-30);

        var locationsQuery = db.Locations.Where(l => l.DeactivatedAt == null);
        if (request.LocationId is not null)
            locationsQuery = locationsQuery.Where(l => l.Id == request.LocationId);
        var locationIds = await locationsQuery.Select(l => l.Id).ToListAsync(cancellationToken);

        var groups = await db.Groups.Where(g => locationIds.Contains(g.LocationId)).ToListAsync(cancellationToken);

        var breaches = new List<BkrBreachResponse>();
        foreach (var group in groups)
        {
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var (dayStartUtc, dayEndUtc) = BelgianCalendarDay.UtcRangeFor(day);

                var attendanceRecords = await db.AttendanceRecords
                    .Where(r => r.LocationId == group.LocationId && r.Date == day && r.CheckInAt != null)
                    .ToListAsync(cancellationToken);

                var childIds = attendanceRecords.Select(r => r.ChildId).Distinct().ToList();
                var groupChildIds = childIds.Count == 0
                    ? []
                    : (await db.ChildGroupAssignments
                        .Where(a => a.GroupId == group.Id && childIds.Contains(a.ChildId)
                                    && a.StartDate <= day && (a.EndDate == null || a.EndDate >= day))
                        .Select(a => a.ChildId)
                        .ToListAsync(cancellationToken))
                        .ToHashSet();

                var deltas = new List<TimestampedDelta>();
                foreach (var record in attendanceRecords.Where(r => groupChildIds.Contains(r.ChildId)))
                {
                    deltas.Add(new TimestampedDelta(record.CheckInAt!.Value, 1, 0));
                    if (record.CheckOutAt is not null)
                        deltas.Add(new TimestampedDelta(record.CheckOutAt.Value, -1, 0));
                }

                var shifts = await db.RoomShifts
                    .Where(s => s.GroupId == group.Id && s.CheckedInAt >= dayStartUtc && s.CheckedInAt < dayEndUtc)
                    .Join(db.StaffProfiles, s => s.StaffProfileId, p => p.Id, (s, p) => new { s.CheckedInAt, s.CheckedOutAt, p.QualificationLevel })
                    .Where(x => x.QualificationLevel != QualificationLevel.StudentVolunteer)
                    .ToListAsync(cancellationToken);

                foreach (var shift in shifts)
                {
                    deltas.Add(new TimestampedDelta(shift.CheckedInAt, 0, 1));
                    if (shift.CheckedOutAt is not null && shift.CheckedOutAt < dayEndUtc)
                        deltas.Add(new TimestampedDelta(shift.CheckedOutAt.Value, 0, -1));
                }

                if (deltas.Count == 0)
                    continue;

                // Batch every delta sharing the exact same instant (e.g. several children
                // checked in together) into one state change — evaluating status after each
                // individual delta within a tied batch would zigzag through intermediate,
                // never-actually-observed states.
                var batches = deltas
                    .GroupBy(d => d.At)
                    .OrderBy(g => g.Key)
                    .Select(g => new TimestampedDelta(g.Key, g.Sum(d => d.PresentDelta), g.Sum(d => d.StaffDelta)))
                    .ToList();

                var present = 0;
                var staff = 0;
                DateTime? breachStartedAt = null;
                DateTime lastEventAt = batches[0].At;

                foreach (var batch in batches)
                {
                    present += batch.PresentDelta;
                    staff += batch.StaffDelta;
                    lastEventAt = batch.At;

                    var threshold = ReportingMapper.ComputeBkrThreshold(staff, isNapTime: false);
                    var status = ReportingMapper.ComputeBkrStatus(present, staff, threshold);

                    if (status == "red" && breachStartedAt is null)
                        breachStartedAt = batch.At;
                    else if (status != "red" && breachStartedAt is not null)
                    {
                        breaches.Add(new BkrBreachResponse(group.Id, group.LocationId, breachStartedAt.Value, batch.At));
                        breachStartedAt = null;
                    }
                }

                if (breachStartedAt is not null)
                {
                    var stillOngoing = day == today;
                    breaches.Add(new BkrBreachResponse(group.Id, group.LocationId, breachStartedAt.Value, stillOngoing ? null : lastEventAt));
                }
            }
        }

        return new BkrBreachHistoryResponse(from, to, breaches);
    }
}
