using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using ChildCare.Api.Cli;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>Feature 013h Foundational (tasks.md T013, research.md R1): the is_platform_admin
/// claim is minted at login time from TenantUser.IsPlatformAdmin, present as exactly "true" only
/// when the flag is set, and entirely absent otherwise (FR-002).</summary>
public class JwtClaimTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Login_DirectorWithoutFlag_TokenHasNoPlatformAdminClaim()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var accessToken = await LoginAsync(client, org.Organisation.Slug, email);

        var claims = ReadClaims(accessToken);
        Assert.DoesNotContain(claims, c => c.Type == "is_platform_admin");
    }

    [Fact]
    public async Task Login_DirectorWithFlag_TokenHasPlatformAdminClaimTrue()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        using (var scope = factory.Services.CreateScope())
        {
            var exitCode = await GrantPlatformAdminCommand.RunAsync(scope.ServiceProvider, email);
            Assert.Equal(0, exitCode);
        }

        var accessToken = await LoginAsync(client, org.Organisation.Slug, email);

        var claims = ReadClaims(accessToken);
        var claim = Assert.Single(claims, c => c.Type == "is_platform_admin");
        Assert.Equal("true", claim.Value);
    }

    private static async Task<string> LoginAsync(HttpClient client, string organisationSlug, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(organisationSlug, email, "password123"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return session.AccessToken;
    }

    private static IReadOnlyCollection<System.Security.Claims.Claim> ReadClaims(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.ToList();
}
