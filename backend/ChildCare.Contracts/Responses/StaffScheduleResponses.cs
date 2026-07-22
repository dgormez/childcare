namespace ChildCare.Contracts.Responses;

// Feature 027 (contracts/staff-app-api.md): isAbsent is removed from the wire shape — it's now
// derivable client-side as `status === "absent"` (research.md R3's computed-property change).
public record StaffScheduleResponse(
    Guid Id,
    Guid StaffProfileId,
    Guid LocationId,
    Guid? GroupId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status,
    string? AbsenceReason,
    Guid? CoverStaffId,
    string? Notes,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CopyWeekSkippedEntryResponse(DateOnly Date, Guid StaffProfileId, string Reason);

public record CopyWeekResponse(int CopiedCount, IReadOnlyList<CopyWeekSkippedEntryResponse> Skipped);

public record ProjectedOnDutyResponse(int ProjectedOnDutyCount, IReadOnlyList<Guid> StaffProfileIds);

public record PublishScheduleWeekResponse(int PublishedCount);

public record SickCoverCandidateResponse(Guid StaffProfileId, string Name, string? QualificationLevel);

public record AssignCoverResponse(StaffScheduleResponse Original, StaffScheduleResponse CoverEntry);
