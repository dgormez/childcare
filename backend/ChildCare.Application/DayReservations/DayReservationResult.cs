using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.DayReservations;

public enum DayReservationFailure
{
    NotFound,

    // Covers both "child doesn't exist" and "child isn't linked to this parent" uniformly
    // (mirrors GetParentDailySummaryQueryHandler's precedent — never let a caller distinguish
    // the two, same reasoning as feature 001's invitation-lookup genericization).
    // PastDate and MissingExchangeDate are NOT handler-level failures: both are pure
    // calendar/presence checks with no DB dependency, so they're caught by
    // SubmitDayReservationCommandValidator (FluentValidation pipeline behaviour, constitution
    // Principle III) before the handler ever runs, surfacing as 422 errors.validation with a
    // fieldErrors entry — same convention as every other validator in this codebase.
    ChildNotLinked,

    // Submission-time only (exchange target date is a published closure day, FR-004) — a bad
    // request, 400.
    NotContractedDay,
    ClosureDay,
    NotPending,
    MissingJustifiedFlag,

    // FR-010/FR-011: an absence approval couldn't resolve which location to write the
    // AttendanceRecord for — no active contract covers RequestedDate's weekday (data-model.md).
    NoContractedLocation,

    // Approval-time only (FR-011): the absence's date became a published closure day between
    // submission and approval — a conflict with server state that's changed since, 409, not the
    // same 400 ClosureDay uses at submission (contracts/day-reservations-api.md documents both
    // status codes separately; this is a genuinely different failure mode, not a duplicate).
    ClosureDayConflict,
}

public class DayReservationResult
{
    public bool Succeeded { get; init; }
    public DayReservationFailure? Failure { get; init; }
    public DayReservationResponse? Response { get; init; }

    public static DayReservationResult Success(DayReservationResponse response) => new() { Succeeded = true, Response = response };
    public static DayReservationResult Fail(DayReservationFailure failure) => new() { Failure = failure };
}

public class ListDayReservationsResult
{
    public bool Succeeded { get; init; }
    public DayReservationFailure? Failure { get; init; }
    public IReadOnlyList<DayReservationResponse> Reservations { get; init; } = [];

    public static ListDayReservationsResult Success(IReadOnlyList<DayReservationResponse> reservations) => new() { Succeeded = true, Reservations = reservations };
    public static ListDayReservationsResult Fail(DayReservationFailure failure) => new() { Failure = failure };
}

public static class DayReservationMapper
{
    public static DayReservationResponse ToResponse(DayReservation entity, string childDisplayName, bool? capacityWarning = null) => new(
        entity.Id,
        entity.ChildId,
        childDisplayName,
        ToWire(entity.Type),
        entity.RequestedDate,
        entity.ExchangeForDate,
        entity.Reason,
        entity.AbsenceJustified,
        ToWire(entity.Status),
        entity.RequestedBy,
        entity.DecidedBy,
        entity.DecidedAt,
        entity.DirectorNotes,
        capacityWarning,
        entity.CreatedAt,
        entity.UpdatedAt);

    public static string ToWire(DayReservationType type) => type.ToString().ToLowerInvariant();

    public static string ToWire(DayReservationStatus status) => status.ToString().ToLowerInvariant();

    public static bool TryParseType(string? value, out DayReservationType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "absence" => Assign(DayReservationType.Absence, out type),
            "extra" => Assign(DayReservationType.Extra, out type),
            "exchange" => Assign(DayReservationType.Exchange, out type),
            _ => false,
        };
    }

    public static bool TryParseStatus(string? value, out DayReservationStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "pending" => Assign(DayReservationStatus.Pending, out status),
            "approved" => Assign(DayReservationStatus.Approved, out status),
            "rejected" => Assign(DayReservationStatus.Rejected, out status),
            "cancelled" => Assign(DayReservationStatus.Cancelled, out status),
            _ => false,
        };
    }

    private static bool Assign(DayReservationType value, out DayReservationType type)
    {
        type = value;
        return true;
    }

    private static bool Assign(DayReservationStatus value, out DayReservationStatus status)
    {
        status = value;
        return true;
    }
}
