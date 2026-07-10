namespace ChildCare.Contracts.Responses;

public record ParentChildResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? PhotoDownloadUrl,
    DateOnly DateOfBirth);
