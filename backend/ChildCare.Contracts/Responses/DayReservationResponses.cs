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
