namespace ChildCare.Contracts.Requests;

public record CreateStaffScheduleRequest(
    Guid StaffProfileId,
    Guid LocationId,
    Guid? GroupId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

public record UpdateStaffScheduleRequest(
    Guid LocationId,
    Guid? GroupId,
    TimeOnly StartTime,
    TimeOnly EndTime);

public record MarkAbsenceRequest(bool IsAbsent, string? AbsenceReason);

public record CopyWeekRequest(Guid LocationId, DateOnly SourceWeekStart, DateOnly TargetWeekStart);
