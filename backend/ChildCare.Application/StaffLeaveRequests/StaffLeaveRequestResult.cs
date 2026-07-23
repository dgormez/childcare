using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.StaffLeaveRequests;

public enum StaffLeaveRequestFailure
{
    ProfileNotFound,
    NotFound,
    AlreadyDecided,
    InvalidDateRange,
}

public class StaffLeaveRequestResult
{
    public bool Succeeded { get; init; }
    public StaffLeaveRequestFailure? Failure { get; init; }
    public StaffLeaveRequestResponse? Response { get; init; }

    public static StaffLeaveRequestResult Success(StaffLeaveRequestResponse response) => new() { Succeeded = true, Response = response };
    public static StaffLeaveRequestResult Fail(StaffLeaveRequestFailure failure) => new() { Failure = failure };
}

public class ListStaffLeaveRequestResult
{
    public bool Succeeded { get; init; }
    public StaffLeaveRequestFailure? Failure { get; init; }
    public IReadOnlyList<StaffLeaveRequestResponse> Entries { get; init; } = [];

    public static ListStaffLeaveRequestResult Success(IReadOnlyList<StaffLeaveRequestResponse> entries) => new() { Succeeded = true, Entries = entries };
    public static ListStaffLeaveRequestResult Fail(StaffLeaveRequestFailure failure) => new() { Failure = failure };
}

public static class StaffLeaveRequestMapper
{
    public static StaffLeaveRequestResponse ToResponse(StaffLeaveRequest entry) => new(
        entry.Id,
        entry.StaffProfileId,
        entry.Type.ToString().ToLowerInvariant(),
        entry.DateFrom,
        entry.DateTo,
        entry.Notes,
        entry.Status.ToString().ToLowerInvariant(),
        entry.DecidedBy,
        entry.DecidedAt,
        entry.CreatedAt);

    public static bool TryParseType(string? value, out StaffLeaveRequestType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "sick" => Assign(StaffLeaveRequestType.Sick, out type),
            "annual" => Assign(StaffLeaveRequestType.Annual, out type),
            "other" => Assign(StaffLeaveRequestType.Other, out type),
            _ => false,
        };
    }

    private static bool Assign(StaffLeaveRequestType value, out StaffLeaveRequestType type)
    {
        type = value;
        return true;
    }
}
