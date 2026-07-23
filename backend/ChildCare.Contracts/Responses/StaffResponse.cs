namespace ChildCare.Contracts.Responses;

public record StaffResponse(
    Guid Id,
    Guid TenantUserId,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Role,
    string? QualificationLevel,
    string? PhotoDownloadUrl,
    IReadOnlyList<Guid> EligibleLocationIds,
    DateTime? DeactivatedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Feature 027 (FR-002) — which weekdays this staff member normally works, e.g. "Monday".
    // Empty = no restriction.
    IReadOnlyList<string> ContractedDays,
    // Feature 028 (FR-010) — which medewerkersbeleid function(s) this staff member may clock
    // in under. Empty = cannot clock in yet.
    IReadOnlyList<string> TimeEntryFunctions);

public record RequestPhotoUploadUrlResponse(
    string UploadUrl,
    string ObjectPath);
