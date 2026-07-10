namespace ChildCare.Contracts.Responses;

public record ParentInvitationResponse(Guid InvitationId, Guid ContactId, string Email, DateTime ExpiresAt);
