using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.StaffTimeEntries;

public static class StaffTimeEntryMapper
{
    public static StaffTimeEntryResponse ToResponse(StaffTimeEntry entry, DateTime utcNow) => new(
        entry.Id,
        entry.StaffProfileId,
        entry.LocationId,
        entry.GroupId,
        entry.ClockedInAt,
        entry.ClockedOutAt,
        entry.Function.ToWireString(),
        entry.Notes,
        entry.IsOpen,
        entry.IsLocked(utcNow),
        entry.UnlockedAt);
}

public enum StaffTimeEntryFailure
{
    ProfileNotFound,
    NotFound,
    AlreadyClockedIn,
    NoOpenEntry,
    NoFunctionConfigured,
    FunctionRequired,
    FunctionNotConfigured,
    LocationNotEligible,
    GroupLocationMismatch,
    Locked,
}

public class StaffTimeEntryResult
{
    public bool Succeeded { get; init; }
    public StaffTimeEntryFailure? Failure { get; init; }
    public StaffTimeEntryResponse? Response { get; init; }
    public bool OverlapWarning { get; init; }

    public static StaffTimeEntryResult Success(StaffTimeEntryResponse response, bool overlapWarning = false) => new()
    {
        Succeeded = true,
        Response = response,
        OverlapWarning = overlapWarning,
    };

    public static StaffTimeEntryResult Fail(StaffTimeEntryFailure failure) => new()
    {
        Succeeded = false,
        Failure = failure,
    };
}
