using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 1 (spec.md, tasks.md T016): POST /api/platform-admin/vaccine-types
/// creates an entry, defaulting sortOrder to max+1 and isActive to true; empty name is rejected
/// (422, via the standard ValidationBehavior pipeline — same convention every other validated
/// command in this codebase uses, e.g. WaitingListEndpoints); the created entry is immediately
/// visible via 013g's unchanged GET /api/vaccine-types (FR-011); denied to a director without
/// the flag (FR-009).</summary>
public class CreateVaccineTypeTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Create_PlatformAdmin_CreatesEntry_DefaultsSortOrderAndActive_VisibleViaTenantReadEndpoint()
    {
        var client = factory.CreateClient();
        var (org, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var existing = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/vaccine-types", accessToken));
        var existingList = (await existing.Content.ReadFromJsonAsync<List<PlatformAdminVaccineTypeResponse>>())!;
        var maxSortOrder = existingList.Max(v => v.SortOrder);

        var name = $"Test Vaccine {Guid.NewGuid():N}";
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest(name, "basisvaccinatieschema")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;
        Assert.Equal(name, created.Name);
        Assert.True(created.IsActive);
        Assert.Equal(maxSortOrder + 1, created.SortOrder);
        Assert.Null(created.DeactivatedByEmail);

        // FR-011: visible immediately via 013g's existing tenant-facing read endpoint, for the
        // same director's own tenant — no propagation delay, no separate cache.
        var tenantRead = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        var tenantList = (await tenantRead.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;
        Assert.Contains(tenantList, v => v.Name == name);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest("", null)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", org.AccessToken,
            new CreateVaccineTypeRequest("Should Not Be Created", null)));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
