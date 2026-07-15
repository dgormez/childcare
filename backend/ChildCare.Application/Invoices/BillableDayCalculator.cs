using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

public record BillableDayResult(
    int PresentDays,
    int UnjustifiedAbsentDays,
    int ClosureDaysExcluded,
    int DaysMin5u,
    int DaysMin11u)
{
    public int SubtotalCents(int dailyRateCents) => (PresentDays + UnjustifiedAbsentDays) * dailyRateCents;
}

/// <summary>
/// Feature 014 — spec.md FR-002, data-model.md's billable-day algorithm. Reads
/// AttendanceRecord alone (research.md R2): Status already absorbs whatever DayReservation
/// (013a) approval and ClosureAttendanceService (011) closure-stamping happened, so no separate
/// query against those tables is needed here.
/// </summary>
public class BillableDayCalculator(ITenantDbContext db)
{
    private const int Min5HourMinutes = 300;
    private const int Min11HourMinutes = 660;

    public async Task<BillableDayResult> ComputeAsync(
        Guid childId, Guid locationId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken)
    {
        var records = await db.AttendanceRecords
            .Where(r => r.ChildId == childId && r.LocationId == locationId && r.Date >= rangeStart && r.Date <= rangeEnd)
            .ToListAsync(cancellationToken);

        var presentDays = records.Count(r => r.Status == AttendanceStatus.Present);
        var unjustifiedAbsentDays = records.Count(r => r.Status == AttendanceStatus.Absent && r.AbsenceJustified == false);
        var closureDaysExcluded = records.Count(r => r.Status == AttendanceStatus.Closure);

        var presentRecords = records.Where(r => r.Status == AttendanceStatus.Present);
        var daysMin5u = presentRecords.Count(r => r.PlannedDurationMinutes >= Min5HourMinutes);
        var daysMin11u = presentRecords.Count(r => r.PlannedDurationMinutes >= Min11HourMinutes);

        return new BillableDayResult(presentDays, unjustifiedAbsentDays, closureDaysExcluded, daysMin5u, daysMin11u);
    }

    /// <summary>
    /// Intersects a contract's active range with the requested calendar month (spec.md FR-002's
    /// mid-month start/end honoring). Returns null if the contract was never active during the
    /// month at all.
    /// </summary>
    public static (DateOnly Start, DateOnly End)? EffectiveRange(Contract contract, int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var start = contract.StartDate > monthStart ? contract.StartDate : monthStart;
        var end = contract.EndDate is { } contractEnd && contractEnd < monthEnd ? contractEnd : monthEnd;

        return start > end ? null : (start, end);
    }
}
