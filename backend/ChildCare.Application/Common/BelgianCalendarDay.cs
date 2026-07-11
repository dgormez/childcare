namespace ChildCare.Application.Common;

/// <summary>
/// Single shared source of truth for the "Europe/Brussels calendar day" boundary used by both
/// <c>ChildEventEditWindowPolicy</c> (FR-006) and <c>GetDailySummaryQuery</c> (FR-018a) — a
/// fixed timezone rather than a per-location one, since <c>Location</c> has no timezone field
/// and the product serves only Belgian KDVs today (research.md R8). Centralized here so the two
/// call sites can never silently drift onto different day-boundary logic (analyze finding C2).
/// </summary>
public static class BelgianCalendarDay
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    /// <summary>Converts a UTC instant to its Europe/Brussels calendar date.</summary>
    public static DateOnly ToLocalDate(DateTime utcInstant)
    {
        var utc = utcInstant.Kind == DateTimeKind.Utc ? utcInstant : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Brussels);
        return DateOnly.FromDateTime(local);
    }

    /// <summary>Today's Europe/Brussels calendar date, evaluated from the server's current instant.</summary>
    public static DateOnly Today() => ToLocalDate(DateTime.UtcNow);

    /// <summary>The UTC instant range [start, end) covering a given Europe/Brussels calendar date.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) UtcRangeFor(DateOnly localDate)
    {
        var startLocal = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, Brussels);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), Brussels);
        return (startUtc, endUtc);
    }

    /// <summary>
    /// The UTC instant range [start, end) covering a given Europe/Brussels calendar month
    /// (feature 009b's Galerij gallery, spec.md Assumptions — same timezone anchor as
    /// <see cref="UtcRangeFor"/>, extended to a month boundary rather than a day one).
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) UtcRangeForMonth(int year, int month)
    {
        var firstDayOfMonth = new DateOnly(year, month, 1);
        var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);
        var (startUtc, _) = UtcRangeFor(firstDayOfMonth);
        var (endUtc, _) = UtcRangeFor(firstDayOfNextMonth);
        return (startUtc, endUtc);
    }
}
