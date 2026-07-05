using ChildCare.Application.Common;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Auth;

/// <summary>Shared "issue and persist a new refresh token" step, reused by every command that
/// mints a session (Login, RefreshToken, GoogleSignIn, AppleSignIn).</summary>
internal static class RefreshTokenFactory
{
    public static string AddRefreshToken(ITenantDbContext db, TenantUser user, IAccessTokenIssuer tokenIssuer)
    {
        var token = tokenIssuer.IssueRefreshToken();
        db.RefreshTokens.Add(new TenantUserRefreshToken
        {
            TenantUserId = user.Id,
            Token        = token,
            ExpiresAt    = DateTime.UtcNow.AddDays(tokenIssuer.RefreshTokenExpiryDays),
        });
        return token;
    }
}
