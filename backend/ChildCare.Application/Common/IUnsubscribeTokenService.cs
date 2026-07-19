namespace ChildCare.Application.Common;

/// <summary>
/// Issues/verifies the signed, purpose-scoped digest-unsubscribe token embedded in every daily
/// report email's footer link (feature 020, research.md R5). Carries no tenant information —
/// tenant-schema resolution happens separately via the link's `org` (organisation slug) query
/// parameter and `OrganisationSlugResolver`, mirroring `ResetPasswordCommand`'s exact pattern
/// (feature 003) — the schema-per-tenant model means this token alone can never be enough to
/// resolve a `Contact`.
/// </summary>
public interface IUnsubscribeTokenService
{
    string CreateToken(Guid contactId);

    /// <summary>Returns null on any tampering/format failure — fails closed, never throws.</summary>
    Guid? TryParseToken(string token);
}
