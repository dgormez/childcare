namespace ChildCare.Contracts.Responses;

public record ChildResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? PhotoDownloadUrl,
    string? Gender,
    string? Nationality,
    string? AllergiesDescription,
    string? AllergySeverity,
    string? MedicalConditions,
    string? DietaryRestrictions,
    string? GpName,
    string? GpPhone,
    string? HealthInsuranceNumber,
    string? Kindcode,
    DateTime? DeactivatedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
