using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

// FR-020: one CSV row per closed time entry, same filter predicate (location/period/closed-only)
// as GetStaffHoursReportQuery, so the two never disagree (research.md R6, verified by
// StaffHoursReportTests' CSV-parity test).
public record ExportStaffHoursReportQuery(Guid LocationId, DateOnly From, DateOnly To) : IRequest<byte[]>;

public class ExportStaffHoursReportQueryHandler(ITenantDbContext db, IStaffHoursCsvWriter csvWriter)
    : IRequestHandler<ExportStaffHoursReportQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportStaffHoursReportQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var entries = await db.StaffTimeEntries
            .Where(e => e.LocationId == request.LocationId
                && e.ClockedInAt >= from && e.ClockedInAt <= to
                && e.ClockedOutAt != null)
            .Join(db.StaffProfiles, e => e.StaffProfileId, p => p.Id, (e, p) => new
            {
                StaffName = p.FirstName + " " + p.LastName,
                e.ClockedInAt,
                e.ClockedOutAt,
                e.Function,
            })
            .ToListAsync(cancellationToken);

        var rows = entries
            .Select(e => new StaffHoursCsvRow(
                e.StaffName,
                DateOnly.FromDateTime(e.ClockedInAt),
                e.Function.ToWireString(),
                e.ClockedInAt,
                e.ClockedOutAt!.Value,
                (decimal)(e.ClockedOutAt!.Value - e.ClockedInAt).TotalHours))
            .ToList();

        return csvWriter.Write(rows);
    }
}
