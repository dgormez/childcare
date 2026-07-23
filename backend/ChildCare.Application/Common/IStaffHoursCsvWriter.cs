namespace ChildCare.Application.Common;

public record StaffHoursCsvRow(string StaffName, DateOnly Date, string Function, DateTime ClockedInAt, DateTime ClockedOutAt, decimal DurationHours);

/// <summary>Port for rendering the medewerkersbeleid subsidy report's raw hours as CSV
/// (research.md R6), mirrors IAttendanceSummaryCsvWriter's shape (feature 018).</summary>
public interface IStaffHoursCsvWriter
{
    byte[] Write(IReadOnlyList<StaffHoursCsvRow> rows);
}
