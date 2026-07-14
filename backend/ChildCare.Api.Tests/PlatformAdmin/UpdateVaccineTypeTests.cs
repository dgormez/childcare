using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 2 (spec.md, tasks.md T030): PATCH /api/platform-admin/vaccine-types/{id}
/// renames/re-categorizes an entry, immediately visible via 013g's unchanged GET
/// /api/vaccine-types (FR-011); a VaccineRecord that already referenced this entry keeps its own
/// originally-saved name text unchanged (013g FR-010, re-verified here); unknown id → 404;
/// director without the flag → 403.</summary>
public class UpdateVaccineTypeTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Update_PlatformAdmin_RenamesAndRecategorizes_VisibleViaTenantReadEndpoint()
    {
        var client = factory.CreateClient();
        var (org, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest($"Original {Guid.NewGuid():N}", "basisvaccinatieschema")));
        var created = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        var newName = $"Renamed {Guid.NewGuid():N}";
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/platform-admin/vaccine-types/{created.Id}", accessToken,
            new UpdateVaccineTypeRequest(newName, "aanbevolen_niet_gratis")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = (await response.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;
        Assert.Equal(newName, updated.Name);
        Assert.Equal("aanbevolen_niet_gratis", updated.Category);

        var tenantRead = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        var tenantList = (await tenantRead.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;
        Assert.Contains(tenantList, v => v.Name == newName);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/platform-admin/vaccine-types/{Guid.NewGuid()}", accessToken,
            new UpdateVaccineTypeRequest("Doesn't Matter", null)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/platform-admin/vaccine-types/{Guid.NewGuid()}", org.AccessToken,
            new UpdateVaccineTypeRequest("Should Not Work", null)));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
