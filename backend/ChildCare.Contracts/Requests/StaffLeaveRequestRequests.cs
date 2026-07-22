namespace ChildCare.Contracts.Requests;

public record CreateLeaveRequestRequest(string Type, DateOnly DateFrom, DateOnly DateTo, string? Notes);

public record DecideLeaveRequestRequest(bool Approve);
