namespace ChildCare.Contracts.Responses;

public record ChildResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? PhotoDownloadUrl,
    string? Gender,
    string? Nationality,
    string? AllergiesDescription,
    string? AllergySeverity,
    string? MedicalConditions,
    string? DietaryRestrictions,
    string? PediatricianName,
    string? PediatricianPhone,
    string? HealthInsuranceNumber,
    string? Kindcode,
    DateTime? DeactivatedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Feature 022. Null for Staff/device-token callers (FR-015, research.md R8) regardless of
    // whether the child is actually verified — see ChildMapper.ToResponse.
    DateTime? IdVerifiedAt,
    string? IdVerifiedByEmail,
    string? IdDocumentType,
    string? IdDocumentNote,
    DateTime? FirstIdVerifiedAt,
    string? FirstIdVerifiedByEmail,
    string? NrnLast4);
