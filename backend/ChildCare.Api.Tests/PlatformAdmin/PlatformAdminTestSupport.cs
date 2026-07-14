using System.Net;
using System.Net.Http.Json;
using ChildCare.Api.Cli;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>Shared setup for feature 013h's platform-admin endpoint tests: registers a fresh
/// director, grants the flag via the real CLI command path, and logs in to obtain a token that
/// actually carries the is_platform_admin claim — every test exercises the real grant→claim
/// pipeline rather than faking the flag directly in the DB.</summary>
internal static class PlatformAdminTestSupport
{
    public static async Task<(RegisterOrganisationResponse Org, string PlatformAdminAccessToken)> RegisterPlatformAdminAsync(
        HttpClient client, IServiceProvider services, string? orgName = null, string? email = null)
    {
        orgName ??= $"Org {Guid.NewGuid():N}";
        email ??= $"director_{Guid.NewGuid():N}@test.com";

        var org = await RegisterOrgAsync(client, orgName, email);

        using (var scope = services.CreateScope())
            Assert.Equal(0, await GrantPlatformAdminCommand.RunAsync(scope.ServiceProvider, email));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(org.Organisation.Slug, email, "password123"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (org, session.AccessToken);
    }
}
