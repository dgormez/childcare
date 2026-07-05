using ChildCare.Application.Common;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChildCare.Infrastructure.Auth;

/// <summary>Moved verbatim from AuthService.VerifyAppleTokenAsync's validation logic (research.md R7).</summary>
public class AppleTokenValidator(IHttpClientFactory httpClientFactory) : IAppleTokenValidator
{
    public async Task<AppleIdentity?> ValidateAsync(string identityToken, string bundleId)
    {
        try
        {
            var http     = httpClientFactory.CreateClient();
            var jwksJson = await http.GetStringAsync("https://appleid.apple.com/auth/keys");
            var jwks     = new JsonWebKeySet(jwksJson);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(identityToken, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = "https://appleid.apple.com",
                ValidateAudience         = true,
                ValidAudience            = bundleId,
                ValidateLifetime         = true,
                IssuerSigningKeys        = jwks.Keys,
                ValidateIssuerSigningKey = true,
            });

            if (!result.IsValid) return null;

            result.Claims.TryGetValue("sub",   out var sub);
            result.Claims.TryGetValue("email", out var email);

            return sub?.ToString() is { } subValue ? new AppleIdentity(subValue, email?.ToString()) : null;
        }
        catch { return null; }
    }
}
