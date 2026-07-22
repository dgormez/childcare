namespace ChildCare.Application.Common;

/// <summary>
/// Issues/verifies the signed, time-limited token embedded in a contract's signing link
/// (feature 024-esignature, research.md R2). Directly mirrors
/// ITourInvitationTokenService's exact shape (feature 023), which itself mirrors
/// IUnsubscribeTokenService (feature 020) — a separate interface per link-purpose, not a
/// shared generic one, matching this codebase's established precedent. Single-use enforcement
/// is a separate concern handled by the caller (Contract.SigningToken column) — a token that
/// still parses successfully here may already have been superseded by a resend/revision/prior
/// signing; see research.md R2.
/// </summary>
public interface IContractSigningTokenService
{
    string CreateToken(Guid contractId);

    /// <summary>Returns null on any tampering/format/expiry failure — fails closed, never throws.</summary>
    Guid? TryParseToken(string token);
}
