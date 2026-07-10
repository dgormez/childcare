using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.WaitingList;

public enum WaitingListFailure
{
    NotFound,
    LocationNotFound,
    ChildNotFound,
    InvalidStatusTransition,
    NotReorderableInCurrentStatus,
    AlreadyAtBoundary,
    InvalidLinkRequest,
}

public class WaitingListEntryResult
{
    public bool Succeeded { get; init; }
    public WaitingListFailure? Failure { get; init; }
    public WaitingListEntryResponse? Response { get; init; }

    public static WaitingListEntryResult Success(WaitingListEntryResponse response) => new() { Succeeded = true, Response = response };
    public static WaitingListEntryResult Fail(WaitingListFailure failure) => new() { Failure = failure };
}

public class ListWaitingListResult
{
    public bool Succeeded { get; init; }
    public WaitingListFailure? Failure { get; init; }
    public IReadOnlyList<WaitingListEntryResponse> Entries { get; init; } = [];

    public static ListWaitingListResult Success(IReadOnlyList<WaitingListEntryResponse> entries) => new() { Succeeded = true, Entries = entries };
    public static ListWaitingListResult Fail(WaitingListFailure failure) => new() { Failure = failure };
}

public class ReorderWaitingListResult
{
    public bool Succeeded { get; init; }
    public WaitingListFailure? Failure { get; init; }
    public IReadOnlyList<WaitingListEntryResponse> Entries { get; init; } = [];

    public static ReorderWaitingListResult Success(IReadOnlyList<WaitingListEntryResponse> entries) => new() { Succeeded = true, Entries = entries };
    public static ReorderWaitingListResult Fail(WaitingListFailure failure) => new() { Failure = failure };
}

public class OccupancyResult
{
    public bool Succeeded { get; init; }
    public WaitingListFailure? Failure { get; init; }
    public IReadOnlyList<OccupancyDayResponse> Days { get; init; } = [];

    public static OccupancyResult Success(IReadOnlyList<OccupancyDayResponse> days) => new() { Succeeded = true, Days = days };
    public static OccupancyResult Fail(WaitingListFailure failure) => new() { Failure = failure };
}

public static class WaitingListMapper
{
    public static WaitingListEntryResponse ToResponse(WaitingListEntry entry, bool isDuplicate) => new(
        entry.Id,
        entry.ChildFirstName,
        entry.ChildLastName,
        entry.DateOfBirth,
        entry.ContactName,
        entry.ContactEmail,
        entry.ContactPhone,
        entry.LocationId,
        entry.RequestedStartDate,
        entry.Priority,
        ToWire(entry.Status),
        entry.Notes,
        entry.ChildId,
        isDuplicate,
        entry.RegisteredAt,
        entry.UpdatedAt);

    public static string ToWire(WaitingListStatus status) => status.ToString().ToLowerInvariant();

    public static bool TryParseStatus(string? value, out WaitingListStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "waiting" => Assign(WaitingListStatus.Waiting, out status),
            "offered" => Assign(WaitingListStatus.Offered, out status),
            "enrolled" => Assign(WaitingListStatus.Enrolled, out status),
            "withdrawn" => Assign(WaitingListStatus.Withdrawn, out status),
            _ => false,
        };
    }

    private static bool Assign(WaitingListStatus value, out WaitingListStatus status)
    {
        status = value;
        return true;
    }
}
