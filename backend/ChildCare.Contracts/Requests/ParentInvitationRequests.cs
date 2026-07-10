namespace ChildCare.Contracts.Requests;

public record CreateParentInvitationRequest(Guid ContactId);

public record AcceptParentInvitationRequest(string OrganisationSlug, string Token, string Password);
