namespace ChildCare.Application.Common;

/// <summary>
/// Port for minting a session's tokens (research.md R8/R4). Implemented in ChildCare.Api by
/// adapting the existing JwtService, so Application/Domain never take a dependency on the
/// legacy auth stack's concrete types. Extended in feature 003 to also carry the account's
/// role claim and to expose refresh-token issuance, so every Application-layer auth command
/// (login, OAuth sign-in, refresh, etc.) mints both tokens through one port. Extended in feature
/// 013h (research.md R1) to also carry the is_platform_admin claim, present only when true.
/// </summary>
public interface IAccessTokenIssuer
{
    string IssueAccessToken(Guid userId, string email, Guid tenantId, string role, bool isPlatformAdmin);

    string IssueRefreshToken();

    int RefreshTokenExpiryDays { get; }
}
