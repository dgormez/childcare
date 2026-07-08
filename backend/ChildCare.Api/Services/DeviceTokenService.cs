using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChildCare.Api.Auth;
using Microsoft.IdentityModel.Tokens;

namespace ChildCare.Api.Services;

/// <summary>
/// Mints device tokens for feature 008a's kiosk-mode tablets — a distinct signing key from
/// JwtService's user-session tokens (research.md R1), so a compromise of one key can never be
/// used to forge the other credential type. Claim names come from DeviceTokenClaims so
/// issuance (here) and validation (Program.cs's DeviceToken scheme) never drift apart.
/// </summary>
public class DeviceTokenService(IConfiguration config)
{
    public int TtlDays => int.Parse(config["DeviceJwt:TtlDays"] ?? "30");

    public int RotateWithinDays => int.Parse(config["DeviceJwt:RotateWithinDays"] ?? "7");

    public string GenerateDeviceToken(Guid tenantId, Guid deviceId, Guid locationId, Guid groupId, int tokenVersion)
    {
        var secret = config["DeviceJwt:Secret"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(DeviceTokenClaims.TenantId, tenantId.ToString()),
            new Claim(DeviceTokenClaims.DeviceId, deviceId.ToString()),
            new Claim(DeviceTokenClaims.LocationId, locationId.ToString()),
            new Claim(DeviceTokenClaims.GroupId, groupId.ToString()),
            new Claim(DeviceTokenClaims.TokenVersion, tokenVersion.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["DeviceJwt:Issuer"],
            audience: config["DeviceJwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(TtlDays),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
