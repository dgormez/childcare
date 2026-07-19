namespace ChildCare.Contracts.Responses;

public record ParentChildResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? PhotoDownloadUrl,
    DateOnly DateOfBirth);

// Feature 030 — contracts/family-siblings-api.md. EnrollmentStart is the child's earliest
// contract start date at any location; EnrollmentEnd is Child.DeactivatedAt.
public record ParentPreviousChildResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? PhotoDownloadUrl,
    DateOnly DateOfBirth,
    DateOnly? EnrollmentStart,
    DateOnly EnrollmentEnd);
