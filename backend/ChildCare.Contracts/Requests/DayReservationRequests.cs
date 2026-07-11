namespace ChildCare.Contracts.Requests;

public record SubmitDayReservationRequest(
    Guid ChildId,
    string Type,
    DateOnly RequestedDate,
    DateOnly? ExchangeForDate,
    string? Reason);

public record ApproveDayReservationRequest(bool? AbsenceJustified);

public record RejectDayReservationRequest(string? DirectorNotes);
