using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 3 (spec.md, tasks.md T041): POST
/// /api/platform-admin/vaccine-types/{id}/reactivate sets IsActive=true and clears all three
/// audit fields; the entry reappears in 013g's unchanged GET /api/vaccine-types (FR-011); a
/// subsequent deactivate produces a fresh audit record (spec.md FR-008); director without the
/// flag → 403.</summary>
public class ReactivateVaccineTypeTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Reactivate_PlatformAdmin_ClearsAuditFields_VisibleAgainViaTenantReadEndpoint()
    {
        var client = factory.CreateClient();
        var (org, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest($"To Reactivate {Guid.NewGuid():N}", null)));
        var created = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/deactivate", accessToken));

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/reactivate", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reactivated = (await response.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;
        Assert.True(reactivated.IsActive);
        Assert.Null(reactivated.DeactivatedByEmail);
        Assert.Null(reactivated.DeactivatedAt);

        var tenantRead = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        var tenantList = (await tenantRead.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;
        Assert.Contains(tenantList, v => v.Id == created.Id);
    }

    [Fact]
    public async Task Reactivate_ThenDeactivateAgain_ProducesFreshAuditRecord()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest($"Redeactivate {Guid.NewGuid():N}", null)));
        var created = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        var firstDeactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/deactivate", accessToken));
        var firstDeactivate = (await firstDeactivateResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/reactivate", accessToken));

        await Task.Delay(50); // ensure a distinguishable timestamp between the two deactivations

        var secondDeactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/deactivate", accessToken));
        var secondDeactivate = (await secondDeactivateResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        Assert.NotNull(secondDeactivate.DeactivatedAt);
        Assert.NotEqual(firstDeactivate.DeactivatedAt, secondDeactivate.DeactivatedAt);
    }

    [Fact]
    public async Task Reactivate_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{Guid.NewGuid()}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
