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
    DateTime? UpdatedAt,
    string Source,
    string? ReferenceCode,
    DateTime? TourProposedAt,
    string TourInvitationStatus,
    DateTime? TourInvitationSentAt,
    string? TourOutcome);

public record OccupancyDayResponse(DateOnly Date, int? FreeCapacity, bool Closed);

// Feature 023 — Digital Online Enrollment

public record GetPublicEnrollmentLocationInfoResponse(string LocationName, bool Enabled, string DefaultLocale);

public record SubmitPublicEnrollmentResponse(string ReferenceCode);
