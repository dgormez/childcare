namespace ChildCare.Contracts.Requests;

public record CreateWaitingListEntryRequest(
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    string ContactName,
    string? ContactEmail,
    string? ContactPhone,
    Guid LocationId,
    DateOnly? RequestedStartDate,
    string? Notes);

public record UpdateWaitingListEntryRequest(
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    string ContactName,
    string? ContactEmail,
    string? ContactPhone,
    Guid LocationId,
    DateOnly? RequestedStartDate,
    string? Notes);

public record ReorderWaitingListEntryRequest(string Direction);

public record TransitionWaitingListStatusRequest(string Status);

public record LinkChildToWaitingListEntryRequest(Guid? ChildId, bool CreateNewChild);
