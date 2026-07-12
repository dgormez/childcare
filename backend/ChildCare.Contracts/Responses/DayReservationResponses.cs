namespace ChildCare.Contracts.Responses;

public record DayReservationResponse(
    Guid Id,
    Guid ChildId,
    string ChildDisplayName,
    string Type,
    DateOnly RequestedDate,
    DateOnly? ExchangeForDate,
    string? Reason,
    bool? AbsenceJustified,
    string Status,
    Guid RequestedBy,
    Guid? DecidedBy,
    DateTime? DecidedAt,
    string? DirectorNotes,
    bool? CapacityWarning,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// Feature 013f — parent-facing read so parent-mobile can decide which entry points to
// show/block without duplicating ReservationPolicyResolver's logic client-side. Keys are
// DayReservationType wire strings (DayReservationMapper.ToWire) — "absence"/"extra"/"exchange".
public record ReservationAvailabilityResponse(
    string Absence,
    string Extra,
    string Exchange,
    int NoticeHours);
