using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

/// <summary>
/// FR-006: monthly present/absent(justified/unjustified)/closure totals per child, rolled up per
/// group and per location. A child's `LocationId`/`GroupId` is resolved per attendance day from
/// that day's own record/assignment (data-model.md's Edge Case) — a mid-month location or group
/// change naturally produces one row per (child, location, group) combination actually spanned,
/// rather than mis-attributing days to wherever the child is today. Shared by the on-screen view
/// and both export formats (research.md R5) — feeding all three from one aggregation is what
/// guarantees their totals agree exactly (spec.md SC-002).
/// </summary>
public record GetAttendanceSummaryQuery(Guid? LocationId, DateOnly Month) : IRequest<AttendanceSummaryResponse>;

public class GetAttendanceSummaryQueryHandler(ITenantDbContext db)
    : IRequestHandler<GetAttendanceSummaryQuery, AttendanceSummaryResponse>
{
    private record RowKey(Guid ChildId, Guid LocationId, Guid? GroupId);

    public async Task<AttendanceSummaryResponse> Handle(GetAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(request.Month.Year, request.Month.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var locationsQuery = db.Locations.Where(l => l.DeactivatedAt == null);
        if (request.LocationId is not null)
            locationsQuery = locationsQuery.Where(l => l.Id == request.LocationId);
        var locationIds = await locationsQuery.Select(l => l.Id).ToListAsync(cancellationToken);

        var records = await db.AttendanceRecords
            .Where(r => locationIds.Contains(r.LocationId) && r.Date >= monthStart && r.Date <= monthEnd)
            .ToListAsync(cancellationToken);

        var childIds = records.Select(r => r.ChildId).Distinct().ToList();
        var assignments = childIds.Count == 0
            ? []
            : await db.ChildGroupAssignments
                .Where(a => childIds.Contains(a.ChildId))
                .ToListAsync(cancellationToken);

        var children = childIds.Count == 0
            ? []
            : await db.Children.Where(c => childIds.Contains(c.Id)).ToListAsync(cancellationToken);
        var childNames = children.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        Guid? ResolveGroupId(Guid childId, DateOnly date) => assignments
            .Where(a => a.ChildId == childId && a.StartDate <= date && (a.EndDate == null || a.EndDate >= date))
            .Select(a => (Guid?)a.GroupId)
            .FirstOrDefault();

        var rows = new Dictionary<RowKey, (int Present, int AbsentJustified, int AbsentUnjustified, int Closure)>();
        foreach (var record in records)
        {
            var key = new RowKey(record.ChildId, record.LocationId, ResolveGroupId(record.ChildId, record.Date));
            var current = rows.GetValueOrDefault(key);

            rows[key] = record.Status switch
            {
                AttendanceStatus.Present => current with { Present = current.Present + 1 },
                AttendanceStatus.Absent when record.AbsenceJustified == true => current with { AbsentJustified = current.AbsentJustified + 1 },
                AttendanceStatus.Absent when record.AbsenceJustified == false => current with { AbsentUnjustified = current.AbsentUnjustified + 1 },
                AttendanceStatus.Closure => current with { Closure = current.Closure + 1 },
                _ => current,
            };
        }

        var childRows = rows.Select(kv => new AttendanceSummaryRowResponse(
            kv.Key.ChildId,
            childNames.GetValueOrDefault(kv.Key.ChildId, string.Empty),
            kv.Key.GroupId,
            kv.Key.LocationId,
            kv.Value.Present,
            kv.Value.AbsentJustified,
            kv.Value.AbsentUnjustified,
            kv.Value.Closure)).ToList();

        var groupTotals = childRows
            .Where(r => r.GroupId is not null)
            .GroupBy(r => r.GroupId!.Value)
            .Select(g => new AttendanceSummaryTotalResponse(
                g.Key,
                g.Sum(r => r.PresentDays),
                g.Sum(r => r.AbsentJustifiedDays),
                g.Sum(r => r.AbsentUnjustifiedDays),
                g.Sum(r => r.ClosureDays)))
            .ToList();

        var locationTotals = childRows
            .GroupBy(r => r.LocationId)
            .Select(g => new AttendanceSummaryTotalResponse(
                g.Key,
                g.Sum(r => r.PresentDays),
                g.Sum(r => r.AbsentJustifiedDays),
                g.Sum(r => r.AbsentUnjustifiedDays),
                g.Sum(r => r.ClosureDays)))
            .ToList();

        return new AttendanceSummaryResponse(monthStart, childRows, groupTotals, locationTotals);
    }
}
