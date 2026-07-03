namespace ChildCare.Application.Common;

/// <summary>
/// Port for minting a ready-to-use access token at the end of registration (research.md R8).
/// Implemented in ChildCare.Api by adapting the existing JwtService, so Application/Domain
/// never take a dependency on the legacy auth stack's concrete types.
/// </summary>
public interface IAccessTokenIssuer
{
    string IssueAccessToken(Guid userId, string email, Guid tenantId);
}
