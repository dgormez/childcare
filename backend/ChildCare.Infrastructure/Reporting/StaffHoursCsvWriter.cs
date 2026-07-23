using System.Globalization;
using System.Text;
using ChildCare.Application.Common;

namespace ChildCare.Infrastructure.Reporting;

/// <summary>
/// RFC 4180-style CSV, UTF-8 with BOM, mirrors CsvAttendanceSummaryWriter's convention (feature
/// 018). FR-020: one row per closed time entry (staff member, date, function, duration),
/// suitable for handoff to a payroll system.
/// </summary>
public class StaffHoursCsvWriter : IStaffHoursCsvWriter
{
    public byte[] Write(IReadOnlyList<StaffHoursCsvRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("StaffName,Date,Function,ClockedInAt,ClockedOutAt,DurationHours");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Escape(row.StaffName),
                row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.Function,
                row.ClockedInAt.ToString("O", CultureInfo.InvariantCulture),
                row.ClockedOutAt.ToString("O", CultureInfo.InvariantCulture),
                // Invariant culture deliberately, not the server's default — a comma decimal
                // separator would silently split this field across two CSV columns (found via
                // this feature's own CSV-parity test, whose assertion failure on a comma-decimal
                // locale is exactly how this was caught).
                row.DurationHours.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        var bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(builder.ToString());
        return [.. bom, .. content];
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
