namespace ChildCare.Contracts.Requests;

// contracts/platform-admin-portal-api.md (feature 032).
public record CreatePlatformAdminInvitationRequest(string Email, string? OrganisationNameNote, string? Locale);
