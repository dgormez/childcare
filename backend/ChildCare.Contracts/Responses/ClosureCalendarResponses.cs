namespace ChildCare.Contracts.Responses;

public record ClosureDeliverySummaryResponse(int Sent, int Failed, int MessageCount);

public record ClosureDayResponse(
    Guid Id,
    Guid LocationId,
    DateOnly Date,
    string Label,
    string ClosureType,
    bool NotifyParents,
    string Status,
    DateTime? NotificationSentAt,
    DateTime? PublishedAt,
    DateTime? CancelledAt,
    ClosureDeliverySummaryResponse DeliverySummary,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ClosureNotificationSummaryResponse(
    int Recipients,
    int PushSent,
    int PushFailed,
    int MessagesCreated);

public record PublishClosureDayResponse(
    ClosureDayResponse Closure,
    int AttendanceRecordsCreated,
    int AttendanceRecordsUpdated,
    bool RequiresAttendanceConfirmation,
    ClosureNotificationSummaryResponse NotificationSummary);

public record CancelClosureDayResponse(
    ClosureDayResponse Closure,
    int AttendanceRecordsReleased,
    int AttendanceRecordsPreserved,
    ClosureNotificationSummaryResponse NotificationSummary);

public record BillableClosureDatesResponse(Guid LocationId, IReadOnlyList<DateOnly> Dates);
