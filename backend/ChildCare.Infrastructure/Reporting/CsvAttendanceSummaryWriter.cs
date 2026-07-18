using System.Text;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;

namespace ChildCare.Infrastructure.Reporting;

/// <summary>
/// RFC 4180-style CSV, UTF-8 with BOM (Excel-on-Windows compatibility for accented NL/FR
/// characters — research.md R8). First CSV export in this codebase, so this establishes the
/// convention rather than following one.
/// </summary>
public class CsvAttendanceSummaryWriter : IAttendanceSummaryCsvWriter
{
    public byte[] Write(AttendanceSummaryResponse summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ChildId,ChildName,GroupId,LocationId,PresentDays,AbsentJustifiedDays,AbsentUnjustifiedDays,ClosureDays");

        foreach (var row in summary.Children)
        {
            builder.AppendLine(string.Join(',',
                row.ChildId,
                Escape(row.ChildName),
                row.GroupId?.ToString() ?? string.Empty,
                row.LocationId,
                row.PresentDays,
                row.AbsentJustifiedDays,
                row.AbsentUnjustifiedDays,
                row.ClosureDays));
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
