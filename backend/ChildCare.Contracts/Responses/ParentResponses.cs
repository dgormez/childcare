namespace ChildCare.Contracts.Responses;

public record ParentChildResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? PhotoDownloadUrl,
    DateOnly DateOfBirth,
    // Feature 021 — FR-004: true when this child has an active contract at a location with QR
    // check-in enabled, so the parent app can hide the "Show code" entry point entirely rather
    // than showing it and failing on tap (contracts/021-qr-checkin/qr-checkin-api.md).
    bool QrCheckInEnabled);

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
