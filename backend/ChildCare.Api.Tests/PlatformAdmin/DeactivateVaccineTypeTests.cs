using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 3 (spec.md, tasks.md T040): POST
/// /api/platform-admin/vaccine-types/{id}/deactivate sets IsActive=false and populates the audit
/// fields from the caller's own identity; the entry no longer appears in 013g's unchanged GET
/// /api/vaccine-types active-only response (FR-011); is a no-op (unchanged audit fields) if
/// already inactive; director without the flag → 403.</summary>
public class DeactivateVaccineTypeTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Deactivate_PlatformAdmin_SetsInactiveWithAuditFields_HiddenFromTenantReadEndpoint()
    {
        var client = factory.CreateClient();
        var (org, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest($"To Deactivate {Guid.NewGuid():N}", null)));
        var created = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/deactivate", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var deactivated = (await response.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;
        Assert.False(deactivated.IsActive);
        Assert.NotNull(deactivated.DeactivatedByEmail);
        Assert.NotNull(deactivated.DeactivatedAt);

        var tenantRead = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        var tenantList = (await tenantRead.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;
        Assert.DoesNotContain(tenantList, v => v.Id == created.Id);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_IsNoOp_PreservesOriginalAuditFields()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/vaccine-types", accessToken,
            new CreateVaccineTypeRequest($"Double Deactivate {Guid.NewGuid():N}", null)));
        var created = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        var firstResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/deactivate", accessToken));
        var first = (await firstResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        await Task.Delay(50); // ensure a redundant call's timestamp would differ if it were (wrongly) reapplied

        var secondResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{created.Id}/deactivate", accessToken));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var second = (await secondResponse.Content.ReadFromJsonAsync<PlatformAdminVaccineTypeResponse>())!;

        // Tolerant, not exact, equality — PostgreSQL's timestamp round-trip precision can differ
        // by a few hundred nanoseconds from the in-memory DateTime.UtcNow value the first
        // response carries (feature 010/013b's same CI-only flake, e.g. ".4072354Z vs
        // ".4072350Z"). A real reapplication would differ by the ~50ms Task.Delay above, not a
        // sub-microsecond rounding artifact, so this tolerance still catches the bug it guards
        // against.
        Assert.NotNull(first.DeactivatedAt);
        Assert.NotNull(second.DeactivatedAt);
        Assert.True((first.DeactivatedAt!.Value - second.DeactivatedAt!.Value).Duration() < TimeSpan.FromSeconds(1));
        Assert.Equal(first.DeactivatedByEmail, second.DeactivatedByEmail);
    }

    [Fact]
    public async Task Deactivate_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{Guid.NewGuid()}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
