using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ChildCare.Api.Models;

namespace ChildCare.Api.Services;

public class JwtService(IConfiguration config)
{
    /// <summary>Generates a short-lived JWT access token for the given user.</summary>
    public string GenerateAccessToken(User user)
        => GenerateAccessToken(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
        ]);

    /// <summary>
    /// Generates a short-lived JWT access token including a tenant_id claim (constitution
    /// Principle I), for the newly-registered director account (research.md R8). Additive
    /// overload — takes primitive claim values instead of the legacy ChildCare.Api.Models.User
    /// type, so ChildCare.Application/Domain never need to reference it.
    /// </summary>
    public string GenerateAccessToken(Guid userId, string email, Guid tenantId)
        => GenerateAccessToken(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("tenant_id", tenantId.ToString()),
        ]);

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
