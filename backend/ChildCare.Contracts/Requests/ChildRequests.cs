namespace ChildCare.Contracts.Requests;

public record CreateChildRequest(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? AllergiesDescription,
    string? AllergySeverity,
    string? MedicalConditions,
    string? DietaryRestrictions,
    string? PediatricianName,
    string? PediatricianPhone,
    string? HealthInsuranceNumber,
    string? Kindcode);

public record UpdateChildRequest(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? AllergiesDescription,
    string? AllergySeverity,
    string? MedicalConditions,
    string? DietaryRestrictions,
    string? PediatricianName,
    string? PediatricianPhone,
    string? HealthInsuranceNumber,
    string? Kindcode);

public record VerifyChildIdentityRequest(
    string DocumentType,
    string? Note);

public record SetChildNrnRequest(string Nrn);

public record CreateContactRequest(
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale);

public record UpdateContactRequest(
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale);

public record VerifyContactIdentityRequest(
    string DocumentType,
    string? Note);

public record LinkContactToChildRequest(
    Guid ContactId,
    string Relationship,
    bool CanPickup,
    bool IsPrimary);

public record UpdateChildContactLinkRequest(
    string Relationship,
    bool CanPickup,
    bool IsPrimary);

public record CreateGroupRequest(
    string Name,
    Guid LocationId);

public record UpdateGroupCapacityRequest(int? Capacity);

public record AssignChildToGroupRequest(
    Guid GroupId,
    DateOnly StartDate);
