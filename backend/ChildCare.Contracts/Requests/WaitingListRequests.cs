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

// Feature 023 — Digital Online Enrollment

public record SubmitPublicEnrollmentRequest(
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    DateOnly? RequestedStartDate,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? Notes,
    string Locale,
    string Website);

public record SendTourInvitationRequest(DateTime ProposedAt);

public record RecordTourOutcomeRequest(string Outcome);
