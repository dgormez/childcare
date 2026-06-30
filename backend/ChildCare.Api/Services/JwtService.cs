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
    {
        var secret  = config["Jwt:Secret"]!;
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry  = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
        };

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
