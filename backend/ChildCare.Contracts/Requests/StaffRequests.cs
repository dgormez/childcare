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
    string? QualificationLevel);

public record AcceptStaffInvitationRequest(string OrganisationSlug, string Token, string Password);
