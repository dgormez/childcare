using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 4 (spec.md, tasks.md T033): GET /api/platform-admin/organisations returns
/// every Tenant with registeredByEmail correctly joined via CreatedFromInvitationId (research.md
/// R5), ordered by createdAt descending; a director without the flag gets 403.</summary>
public class ListPlatformAdminOrganisationsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task List_ReturnsOrganisation_WithRegisteredByEmail()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var orgName = $"Directory Org {Guid.NewGuid():N}";
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, orgName, directorEmail);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/organisations", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organisations = (await response.Content.ReadFromJsonAsync<List<PlatformAdminOrganisationResponse>>())!;

        var found = organisations.Single(o => o.Id == org.Organisation.Id);
        Assert.Equal(orgName, found.Name);
        Assert.Equal(directorEmail, found.RegisteredByEmail);
        Assert.Equal("ready", found.ProvisioningStatus);
    }

    [Fact]
    public async Task List_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/organisations", org.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
