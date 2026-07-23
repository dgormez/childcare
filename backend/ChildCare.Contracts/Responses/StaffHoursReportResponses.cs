namespace ChildCare.Contracts.Responses;

public record StaffHoursByFunctionResponse(string Function, decimal TotalStaffHours, decimal? Ratio);

public record StaffHoursReportResponse(
    Guid LocationId,
    DateOnly From,
    DateOnly To,
    decimal TotalChildHours,
    IReadOnlyList<StaffHoursByFunctionResponse> ByFunction);
