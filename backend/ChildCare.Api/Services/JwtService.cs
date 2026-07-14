using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ChildCare.Api.Services;

public class JwtService(IConfiguration config)
{
    /// <summary>
    /// Generates a short-lived JWT access token including a tenant_id claim (constitution
    /// Principle I) and a role claim (research.md R4, FR-011) — takes primitive claim values
    /// instead of the legacy ChildCare.Api.Models.User type, so ChildCare.Application/Domain
    /// never need to reference it. The role is carried as ClaimTypes.Role specifically (not a
    /// custom claim name) so ASP.NET Core's RequireRole()/User.IsInRole() work against it with
    /// no custom claim-matching logic (research.md R5).
    ///
    /// Feature 013h (research.md R1, FR-002): the is_platform_admin claim is present, as exactly
    /// "true", only when isPlatformAdmin is true — omitted entirely otherwise (never present as
    /// "false" or any other value), matching FR-002's "claim absence == not a platform admin"
    /// requirement and keeping the token minimal for the common case. Because it's baked in at
    /// issuance, revoking IsPlatformAdmin only takes effect on the account's next
    /// login/refresh — an accepted, explicit trade-off (FR-002), not a gap.
    /// </summary>
    public string GenerateAccessToken(Guid userId, string email, Guid tenantId, string role, bool isPlatformAdmin)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, role),
        ];

        if (isPlatformAdmin)
            claims.Add(new Claim("is_platform_admin", "true"));

        return GenerateAccessToken(claims.ToArray());
    }

    private string GenerateAccessToken(Claim[] claims)
    {
        var secret  = config["Jwt:Secret"]!;
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry  = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var token = new JwtSecurityToken(
            issuer:            config["Jwt:Issuer"],
            audience:          config["Jwt:Audience"],
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Generates a cryptographically secure refresh token.</summary>
    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public int RefreshTokenExpiryDays =>
        int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "30");
}
