using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>Feature 013h (FR-009, tasks.md T053 — speckit-converge finding): the
/// PlatformAdminOnly policy's is_platform_admin claim check is additive on top of, never a
/// substitute for, DirectorOnly-equivalent role authorization. No real account can ever produce
/// this token shape (the flag is only ever granted alongside the existing director role — see
/// GrantPlatformAdminCommand), so this hand-mints one directly, mirroring
/// KioskModeTestSupport.IssueExpiredDeviceToken's same rationale for testing a shape the real
/// app can never itself produce.</summary>
public class PlatformAdminOnlyPolicyTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task PlatformAdminClaim_WithoutDirectorRole_IsRejected()
    {
        var client = factory.CreateClient();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "not-a-director@test.com"),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "staff"),
            new Claim("is_platform_admin", "true"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebAppFactoryBase.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestWebAppFactoryBase.TestJwtIssuer,
            audience: TestWebAppFactoryBase.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform-admin/vaccine-types");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
