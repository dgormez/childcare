using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Common;

/// <summary>Port for rendering the monthly attendance summary as CSV (research.md R8).</summary>
public interface IAttendanceSummaryCsvWriter
{
    byte[] Write(AttendanceSummaryResponse summary);
}
