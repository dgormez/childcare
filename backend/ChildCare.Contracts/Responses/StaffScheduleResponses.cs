namespace ChildCare.Contracts.Responses;

public record StaffScheduleResponse(
    Guid Id,
    Guid StaffProfileId,
    Guid LocationId,
    Guid? GroupId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsAbsent,
    string? AbsenceReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CopyWeekSkippedEntryResponse(DateOnly Date, Guid StaffProfileId, string Reason);

public record CopyWeekResponse(int CopiedCount, IReadOnlyList<CopyWeekSkippedEntryResponse> Skipped);

public record ProjectedOnDutyResponse(int ProjectedOnDutyCount, IReadOnlyList<Guid> StaffProfileIds);
