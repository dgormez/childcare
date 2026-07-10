using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.StaffScheduling;

public enum StaffScheduleFailure
{
    NotFound,
    StaffNotFound,
    LocationNotFound,
    GroupNotFound,
    NotEligible,
    Overlap,
    Duplicate,
    PastDate,
    InvalidCopyTarget,
}

public class ListStaffScheduleResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public IReadOnlyList<StaffScheduleResponse> Entries { get; init; } = [];

    public static ListStaffScheduleResult Success(IReadOnlyList<StaffScheduleResponse> entries) => new()
    {
        Succeeded = true,
        Entries = entries,
    };

    public static ListStaffScheduleResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public class StaffScheduleResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public StaffScheduleResponse? Response { get; init; }

    public static StaffScheduleResult Success(StaffScheduleResponse response) => new() { Succeeded = true, Response = response };
    public static StaffScheduleResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public class DeleteStaffScheduleResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }

    public static DeleteStaffScheduleResult Success() => new() { Succeeded = true };
    public static DeleteStaffScheduleResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public class CopyWeekResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public CopyWeekResponse? Response { get; init; }

    public static CopyWeekResult Success(CopyWeekResponse response) => new() { Succeeded = true, Response = response };
    public static CopyWeekResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public class ProjectedOnDutyResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public ProjectedOnDutyResponse? Response { get; init; }

    public static ProjectedOnDutyResult Success(ProjectedOnDutyResponse response) => new() { Succeeded = true, Response = response };
    public static ProjectedOnDutyResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public static class StaffScheduleMapper
{
    public static StaffScheduleResponse ToResponse(StaffSchedule entry) => new(
        entry.Id,
        entry.StaffProfileId,
        entry.LocationId,
        entry.GroupId,
        entry.Date,
        entry.StartTime,
        entry.EndTime,
        entry.IsAbsent,
        entry.AbsenceReason is null ? null : ToWire(entry.AbsenceReason.Value),
        entry.CreatedAt,
        entry.UpdatedAt);

    public static string ToWire(AbsenceReason reason) => reason.ToString().ToLowerInvariant();

    public static bool TryParseAbsenceReason(string? value, out AbsenceReason reason)
    {
        reason = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "sick" => Assign(AbsenceReason.Sick, out reason),
            "leave" => Assign(AbsenceReason.Leave, out reason),
            "holiday" => Assign(AbsenceReason.Holiday, out reason),
            _ => false,
        };
    }

    private static bool Assign(AbsenceReason value, out AbsenceReason reason)
    {
        reason = value;
        return true;
    }
}
