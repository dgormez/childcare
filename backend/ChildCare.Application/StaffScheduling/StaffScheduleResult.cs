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
    // Feature 027 additions.
    ProfileNotFound,
    InvalidWeekStart,
    NoAbsentAssignment,
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

public class PublishScheduleWeekResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public PublishScheduleWeekResponse? Response { get; init; }

    public static PublishScheduleWeekResult Success(PublishScheduleWeekResponse response) => new() { Succeeded = true, Response = response };
    public static PublishScheduleWeekResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

// FR-005/FR-005a: 200 with the updated row if one existed for the resolved date, 204 (no
// Response, still Succeeded) if the staff member had no assignment that day.
public class ReportSickResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public bool HadAssignment { get; init; }
    public StaffScheduleResponse? Response { get; init; }

    public static ReportSickResult Success(StaffScheduleResponse? response) => new()
    {
        Succeeded = true,
        HadAssignment = response is not null,
        Response = response,
    };

    public static ReportSickResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public class SickCoverCandidatesResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public IReadOnlyList<SickCoverCandidateResponse> Candidates { get; init; } = [];

    public static SickCoverCandidatesResult Success(IReadOnlyList<SickCoverCandidateResponse> candidates) => new()
    {
        Succeeded = true,
        Candidates = candidates,
    };

    public static SickCoverCandidatesResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
}

public class AssignCoverResult
{
    public bool Succeeded { get; init; }
    public StaffScheduleFailure? Failure { get; init; }
    public AssignCoverResponse? Response { get; init; }

    public static AssignCoverResult Success(AssignCoverResponse response) => new() { Succeeded = true, Response = response };
    public static AssignCoverResult Fail(StaffScheduleFailure failure) => new() { Failure = failure };
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
        ToWire(entry.Status),
        entry.AbsenceReason is null ? null : ToWire(entry.AbsenceReason.Value),
        entry.CoverStaffId,
        entry.Notes,
        entry.IsPublished,
        entry.CreatedAt,
        entry.UpdatedAt);

    public static string ToWire(AbsenceReason reason) => reason.ToString().ToLowerInvariant();

    public static string ToWire(StaffScheduleStatus status) => status.ToString().ToLowerInvariant();

    // research.md R3: sick -> Sick, annual -> Leave, other -> Leave.
    public static AbsenceReason ToAbsenceReason(StaffLeaveRequestType type) => type switch
    {
        StaffLeaveRequestType.Sick => AbsenceReason.Sick,
        StaffLeaveRequestType.Annual => AbsenceReason.Leave,
        StaffLeaveRequestType.Other => AbsenceReason.Leave,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

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
