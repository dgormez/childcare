namespace ChildCare.Contracts.Requests;

public record RegisterOrganisationRequest(
    string InvitationToken,
    string OrganisationName,
    string DirectorName,
    string Email,
    string Password);
