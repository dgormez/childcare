using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.ClosureCalendar;

public enum ClosureCalendarFailure
{
    NotFound,
    LocationNotFound,
    DuplicateDate,
    PastDate,
    NotEditable,
    NotPublishable,
    AttendanceConfirmationRequired,
}

public class ListClosureCalendarResult
{
    public bool Succeeded { get; init; }
    public ClosureCalendarFailure? Failure { get; init; }
    public IReadOnlyList<ClosureDayResponse> Closures { get; init; } = [];

    public static ListClosureCalendarResult Success(IReadOnlyList<ClosureDayResponse> closures) => new()
    {
        Succeeded = true,
        Closures = closures,
    };

    public static ListClosureCalendarResult Fail(ClosureCalendarFailure failure) => new() { Failure = failure };
}

public class ClosureCalendarResult
{
    public bool Succeeded { get; init; }
    public ClosureCalendarFailure? Failure { get; init; }
    public ClosureDayResponse? Response { get; init; }
    public int CheckedInCount { get; init; }

    public static ClosureCalendarResult Success(ClosureDayResponse response) => new() { Succeeded = true, Response = response };
    public static ClosureCalendarResult Fail(ClosureCalendarFailure failure, int checkedInCount = 0) => new()
    {
        Failure = failure,
        CheckedInCount = checkedInCount,
    };
}

public class PublishClosureCalendarResult
{
    public bool Succeeded { get; init; }
    public ClosureCalendarFailure? Failure { get; init; }
    public PublishClosureDayResponse? Response { get; init; }
    public int CheckedInCount { get; init; }

    public static PublishClosureCalendarResult Success(PublishClosureDayResponse response) => new() { Succeeded = true, Response = response };
    public static PublishClosureCalendarResult Fail(ClosureCalendarFailure failure, int checkedInCount = 0) => new()
    {
        Failure = failure,
        CheckedInCount = checkedInCount,
    };
}

public class CancelClosureCalendarResult
{
    public bool Succeeded { get; init; }
    public bool RemovedDraft { get; init; }
    public ClosureCalendarFailure? Failure { get; init; }
    public CancelClosureDayResponse? Response { get; init; }

    public static CancelClosureCalendarResult DraftRemoved() => new() { Succeeded = true, RemovedDraft = true };
    public static CancelClosureCalendarResult Success(CancelClosureDayResponse response) => new() { Succeeded = true, Response = response };
    public static CancelClosureCalendarResult Fail(ClosureCalendarFailure failure) => new() { Failure = failure };
}

public static class ClosureCalendarMapper
{
    public static ClosureDayResponse ToResponse(KdvClosureDay closure, IReadOnlyList<ClosureNotificationDelivery>? deliveries = null)
    {
        var sent = deliveries?.Count(d => d.PushStatus == ClosureDeliveryStatus.Sent) ?? 0;
        var failed = deliveries?.Count(d => d.PushStatus == ClosureDeliveryStatus.Failed) ?? 0;
        var messages = deliveries?.Count(d => d.MessageId is not null) ?? 0;
        return new ClosureDayResponse(
            closure.Id,
            closure.LocationId,
            closure.Date,
            closure.Label,
            ToWire(closure.ClosureType),
            closure.NotifyParents,
            ToWire(closure.Status),
            closure.NotificationSentAt,
            closure.PublishedAt,
            closure.CancelledAt,
            new ClosureDeliverySummaryResponse(sent, failed, messages),
            closure.CreatedAt,
            closure.UpdatedAt);
    }

    public static string ToWire(ClosureType type) => type.ToString().ToLowerInvariant();
    public static string ToWire(ClosureStatus status) => status.ToString().ToLowerInvariant();

    public static bool TryParseClosureType(string value, out ClosureType type)
    {
        type = default;
        return value.Trim().ToLowerInvariant() switch
        {
            "holiday" => Assign(ClosureType.Holiday, out type),
            "training" => Assign(ClosureType.Training, out type),
            "extraordinary" => Assign(ClosureType.Extraordinary, out type),
            _ => false,
        };
    }

    private static bool Assign(ClosureType value, out ClosureType type)
    {
        type = value;
        return true;
    }
}
