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
    DateTime UpdatedAt);

public record RequestPhotoUploadUrlResponse(
    string UploadUrl,
    string ObjectPath);
