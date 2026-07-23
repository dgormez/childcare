using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

// FR-016/FR-017/FR-018/FR-019: the medewerkersbeleid subsidy report — child-hours from closed
// AttendanceRecords, staff-hours per function from closed StaffTimeEntries, both for one
// location/period. Displays computed ratios only, no pass/fail evaluation against Opgroeien's
// thresholds (spec.md Clarifications — that's feature 041's job).
public record GetStaffHoursReportQuery(Guid LocationId, DateOnly From, DateOnly To) : IRequest<StaffHoursReportResponse>;

public class GetStaffHoursReportQueryHandler(ITenantDbContext db) : IRequestHandler<GetStaffHoursReportQuery, StaffHoursReportResponse>
{
    public async Task<StaffHoursReportResponse> Handle(GetStaffHoursReportQuery request, CancellationToken cancellationToken)
    {
        // FR-017: only fully-closed attendance records count — an open (still-present) record's
        // duration is unknown, not zero (research.md R5, mirrors FR-019's staff-hours treatment).
        var attendanceRecords = await db.AttendanceRecords
            .Where(a => a.LocationId == request.LocationId
                && a.Date >= request.From && a.Date <= request.To
                && a.CheckInAt != null && a.CheckOutAt != null)
            .Select(a => new { a.CheckInAt, a.CheckOutAt })
            .ToListAsync(cancellationToken);

        var totalChildHours = attendanceRecords.Sum(a => (decimal)(a.CheckOutAt!.Value - a.CheckInAt!.Value).TotalHours);

        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        // FR-019: an open time entry is excluded from the totals rather than estimated.
        var timeEntries = await db.StaffTimeEntries
            .Where(e => e.LocationId == request.LocationId
                && e.ClockedInAt >= from && e.ClockedInAt <= to
                && e.ClockedOutAt != null)
            .Select(e => new { e.Function, e.ClockedInAt, e.ClockedOutAt })
            .ToListAsync(cancellationToken);

        var byFunction = Enum.GetValues<StaffTimeEntryFunction>()
            .Select(function =>
            {
                var totalStaffHours = timeEntries
                    .Where(e => e.Function == function)
                    .Sum(e => (decimal)(e.ClockedOutAt!.Value - e.ClockedInAt).TotalHours);

                // FR-016 Acceptance Scenario 2: no divide-by-zero — a null ratio, not an error.
                var ratio = totalStaffHours == 0 ? (decimal?)null : totalChildHours / totalStaffHours;

                return new StaffHoursByFunctionResponse(function.ToWireString(), totalStaffHours, ratio);
            })
            .ToList();

        return new StaffHoursReportResponse(request.LocationId, request.From, request.To, totalChildHours, byFunction);
    }
}
