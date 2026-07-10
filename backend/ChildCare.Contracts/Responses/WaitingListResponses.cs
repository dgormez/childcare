namespace ChildCare.Contracts.Responses;

public record WaitingListEntryResponse(
    Guid Id,
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    string ContactName,
    string? ContactEmail,
    string? ContactPhone,
    Guid LocationId,
    DateOnly? RequestedStartDate,
    int Priority,
    string Status,
    string? Notes,
    Guid? ChildId,
    bool IsDuplicate,
    DateTime RegisteredAt,
    DateTime? UpdatedAt);

public record OccupancyDayResponse(DateOnly Date, int? FreeCapacity, bool Closed);
