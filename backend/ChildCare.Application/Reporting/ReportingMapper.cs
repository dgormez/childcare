namespace ChildCare.Application.Reporting;

/// <summary>
/// Shared status/threshold computations reused across every reporting query — occupancy
/// colour-coding (spec.md FR-020) and the BKR per-caregiver-cap/threshold logic
/// <see cref="ChildCare.Application.Attendance.GetBkrRatioQuery"/> already established for the
/// location-scoped live ratio (research.md R2), applied here at group scope and in breach-history
/// reconstruction (research.md R3).
/// </summary>
public static class ReportingMapper
{
    /// <summary>
    /// FR-020: green when under capacity, amber when exactly at capacity, red when over. Null
    /// when capacity itself is unset (a group with no <c>Capacity</c> configured — Edge Cases).
    /// </summary>
    public static string? ComputeOccupancyStatus(int presentCount, int? capacity)
    {
        if (capacity is null)
            return null;

        if (presentCount < capacity)
            return "green";
        if (presentCount == capacity)
            return "amber";
        return "red";
    }

    /// <summary>Mirrors <see cref="ChildCare.Application.Attendance.GetBkrRatioQuery"/>'s per-caregiver cap.</summary>
    public static int ComputeBkrPerCaregiverCap(int qualifiedStaffCount, bool isNapTime) =>
        isNapTime ? 14 : (qualifiedStaffCount <= 1 ? 8 : 9);

    public static int ComputeBkrThreshold(int qualifiedStaffCount, bool isNapTime) =>
        ComputeBkrPerCaregiverCap(qualifiedStaffCount, isNapTime) * Math.Max(qualifiedStaffCount, 1);

    /// <summary>Mirrors <see cref="ChildCare.Application.Attendance.GetBkrRatioQuery"/>'s status rule exactly.</summary>
    public static string ComputeBkrStatus(int presentCount, int qualifiedStaffCount, int threshold)
    {
        if (qualifiedStaffCount == 0 && presentCount > 0)
            return "red";
        if (presentCount < threshold)
            return "green";
        if (presentCount == threshold)
            return "amber";
        return "red";
    }

    public static int ComputeDaysOverdue(DateOnly dueDate, DateOnly today) =>
        Math.Max(0, today.DayNumber - dueDate.DayNumber);
}
