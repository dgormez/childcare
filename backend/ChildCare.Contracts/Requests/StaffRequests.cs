namespace ChildCare.Contracts.Requests;

public record CreateStaffProfileRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? QualificationLevel,
    string Role,
    Guid? ExistingTenantUserId);

public record UpdateStaffProfileRequest(
    string FirstName,
    string LastName,
    string Phone,
    string? QualificationLevel,
    // Feature 027 (FR-002, data-model.md) — null leaves ContractedDays unchanged; an empty
    // array explicitly clears it back to "no restriction".
    IReadOnlyList<string>? ContractedDays = null);

public record AcceptStaffInvitationRequest(string OrganisationSlug, string Token, string Password);

public record SetCaregiverPinRequest(string Pin);
