namespace ChildCare.Application.Common;

/// <summary>
/// Issues/verifies the signed, purpose-scoped token embedded in a tour invitation's
/// accept/decline links (feature 023, research.md R5). Directly mirrors
/// IUnsubscribeTokenService's exact shape (feature 020) — a separate interface, not a shared
/// generic one, since that's the established precedent for a single link-purpose token in this
/// codebase. Carries no tenant information — tenant-schema resolution happens separately via
/// the link's `org` (organisation slug) query parameter and `OrganisationSlugResolver`.
/// </summary>
public interface ITourInvitationTokenService
{
    string CreateToken(Guid waitingListEntryId);

    /// <summary>Returns null on any tampering/format failure — fails closed, never throws.</summary>
    Guid? TryParseToken(string token);
}
