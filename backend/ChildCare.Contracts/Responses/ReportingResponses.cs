namespace ChildCare.Contracts.Responses;

public record OccupancyGroupSummaryResponse(
    Guid GroupId,
    string GroupName,
    int PresentCount,
    int? Capacity,
    string? Status);

public record OccupancyLocationSummaryResponse(
    Guid LocationId,
    string LocationName,
    int PresentCount,
    int Capacity,
    string Status,
    IReadOnlyList<OccupancyGroupSummaryResponse> Groups,
    IReadOnlyList<OccupancyDayResponse> WeekAhead);

public record OccupancySummaryResponse(DateOnly AsOf, IReadOnlyList<OccupancyLocationSummaryResponse> Locations);

public record BkrGroupRatioResponse(
    Guid GroupId,
    Guid LocationId,
    int PresentCount,
    int QualifiedStaffCount,
    bool IsNapTime,
    int Threshold,
    string Status);

public record BkrRatioOverviewResponse(DateTime AsOf, IReadOnlyList<BkrGroupRatioResponse> Groups);

public record BkrBreachResponse(Guid GroupId, Guid LocationId, DateTime StartedAt, DateTime? EndedAt);

public record BkrBreachHistoryResponse(DateOnly From, DateOnly To, IReadOnlyList<BkrBreachResponse> Breaches);

public record AttendanceSummaryRowResponse(
    Guid ChildId,
    string ChildName,
    Guid? GroupId,
    Guid LocationId,
    int PresentDays,
    int AbsentJustifiedDays,
    int AbsentUnjustifiedDays,
    int ClosureDays);

public record AttendanceSummaryTotalResponse(
    Guid Id,
    int PresentDays,
    int AbsentJustifiedDays,
    int AbsentUnjustifiedDays,
    int ClosureDays);

public record AttendanceSummaryResponse(
    DateOnly Month,
    IReadOnlyList<AttendanceSummaryRowResponse> Children,
    IReadOnlyList<AttendanceSummaryTotalResponse> GroupTotals,
    IReadOnlyList<AttendanceSummaryTotalResponse> LocationTotals);

public record OverdueInvoiceResponse(Guid InvoiceId, string ChildName, DateOnly DueDate, int DaysOverdue, int TotalCents);

public record InvoiceStatusOverviewResponse(
    DateOnly Month,
    int PaidCount,
    int PaidTotalCents,
    int OutstandingCount,
    int OutstandingTotalCents,
    int OverdueCount,
    int OverdueTotalCents,
    int TotalInvoicedCents,
    IReadOnlyList<OverdueInvoiceResponse> OverdueInvoices);

public record DataCompletenessFlagResponse(
    string Type,
    string SubjectType,
    Guid SubjectId,
    string SubjectName,
    string? Detail);

public record DataCompletenessResponse(IReadOnlyList<DataCompletenessFlagResponse> Flags);
