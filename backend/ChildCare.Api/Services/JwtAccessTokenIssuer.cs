using ChildCare.Application.Common;

namespace ChildCare.Api.Services;

/// <summary>Adapts the existing JwtService to the IAccessTokenIssuer port (research.md R8).</summary>
public class JwtAccessTokenIssuer(JwtService jwtService) : IAccessTokenIssuer
{
    public string IssueAccessToken(Guid userId, string email, Guid tenantId)
        => jwtService.GenerateAccessToken(userId, email, tenantId);
}
