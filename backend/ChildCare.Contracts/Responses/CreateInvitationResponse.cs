namespace ChildCare.Contracts.Responses;

/// <summary>
/// `Token` is the plaintext, single-use invitation token — returned exactly once, here.
/// Only its hash is ever persisted (contracts/create-invitation.md, research.md R4).
/// </summary>
public record CreateInvitationResponse(Guid InvitationId, string Email, string Token, DateTime ExpiresAt);
