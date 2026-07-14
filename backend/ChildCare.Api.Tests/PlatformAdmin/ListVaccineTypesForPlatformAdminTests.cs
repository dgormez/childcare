using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 1 (spec.md, tasks.md T015): GET /api/platform-admin/vaccine-types returns
/// every catalog entry — active and inactive — with audit fields; denied to a director without
/// the platform-admin flag (FR-009).</summary>
public class ListVaccineTypesForPlatformAdminTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task List_PlatformAdmin_ReturnsFullCatalog_WithAuditFields()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/vaccine-types", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = (await response.Content.ReadFromJsonAsync<List<PlatformAdminVaccineTypeResponse>>())!;

        // 013g seeds 9 active entries (VaccineTypeListTests) — the platform-admin list must
        // include every one of them too, not a subset.
        Assert.True(list.Count >= 9);
        Assert.All(list, v => Assert.True(v.IsActive));
        Assert.All(list, v => Assert.Null(v.DeactivatedByEmail));
        Assert.All(list, v => Assert.Null(v.DeactivatedAt));
    }

    [Fact]
    public async Task List_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/vaccine-types", org.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
