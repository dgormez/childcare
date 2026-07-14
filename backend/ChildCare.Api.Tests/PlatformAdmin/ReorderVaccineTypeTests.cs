using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 2 (spec.md, tasks.md T031): POST
/// /api/platform-admin/vaccine-types/{id}/reorder swaps adjacent sortOrder values within the
/// entry's own category, immediately visible via 013g's unchanged GET /api/vaccine-types
/// (FR-011); "up" on the first entry / "down" on the last both return 400; unknown id → 404;
/// director without the flag → 403.</summary>
public class ReorderVaccineTypeTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Reorder_PlatformAdmin_SwapsAdjacentEntries_VisibleViaTenantReadEndpoint()
    {
        var client = factory.CreateClient();
        var (org, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        // 013g seeds 9 entries across 2 categories (VaccineTypeListTests) — every category has
        // at least 2 entries to reorder within.
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/vaccine-types", accessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<PlatformAdminVaccineTypeResponse>>())!;
        var category = list.GroupBy(v => v.Category).First(g => g.Count() >= 2).Key;
        var queue = list.Where(v => v.Category == category).OrderBy(v => v.SortOrder).ToList();

        var second = queue[1];
        var first = queue[0];

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{second.Id}/reorder", accessToken,
            new ReorderVaccineTypeRequest("up")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reordered = (await response.Content.ReadFromJsonAsync<List<PlatformAdminVaccineTypeResponse>>())!;
        var newFirst = reordered.Single(v => v.Id == first.Id);
        var newSecond = reordered.Single(v => v.Id == second.Id);
        Assert.Equal(first.SortOrder, newSecond.SortOrder);
        Assert.Equal(second.SortOrder, newFirst.SortOrder);

        // FR-011: reflected immediately via 013g's tenant-facing read endpoint's ordering.
        var tenantRead = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        var tenantList = (await tenantRead.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;
        var tenantEntry = tenantList.Single(v => v.Name == second.Name);
        Assert.Equal(newSecond.SortOrder, tenantEntry.SortOrder);
    }

    [Fact]
    public async Task Reorder_UpOnFirstEntry_Returns400()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/vaccine-types", accessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<PlatformAdminVaccineTypeResponse>>())!;
        var category = list.GroupBy(v => v.Category).First(g => g.Count() >= 2).Key;
        var first = list.Where(v => v.Category == category).OrderBy(v => v.SortOrder).First();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{first.Id}/reorder", accessToken,
            new ReorderVaccineTypeRequest("up")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_DownOnLastEntry_Returns400()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/vaccine-types", accessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<PlatformAdminVaccineTypeResponse>>())!;
        var category = list.GroupBy(v => v.Category).First(g => g.Count() >= 2).Key;
        var last = list.Where(v => v.Category == category).OrderBy(v => v.SortOrder).Last();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{last.Id}/reorder", accessToken,
            new ReorderVaccineTypeRequest("down")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_UnknownId_Returns404()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{Guid.NewGuid()}/reorder", accessToken,
            new ReorderVaccineTypeRequest("up")));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{Guid.NewGuid()}/reorder", org.AccessToken,
            new ReorderVaccineTypeRequest("up")));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
