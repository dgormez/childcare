using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 1 (spec.md): a vaccine record's reference to a catalog entry, and
/// spec.md FR-010's guarantee that a later-deactivated entry never breaks an existing record.</summary>
public class VaccineTypeReferenceTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<Guid> GetHpvVaccineTypeIdAsync(HttpClient client, string accessToken)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", accessToken));
        var list = (await response.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;
        return list.Single(v => v.Name == "HPV").Id;
    }

    [Fact]
    public async Task CreateVaccineRecord_WithVaccineTypeId_StoresReference_AndSurvivesDeactivation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var hpvId = await GetHpvVaccineTypeIdAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("HPV", null, new DateOnly(2026, 6, 1), null, null, null, hpvId)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;
        Assert.Equal(hpvId, record.VaccineTypeId);

        // Deactivate the catalog entry directly (platform-operator action — no director-facing
        // write path exists for this, spec.md FR-009).
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            var vaccineType = await publicDb.VaccineTypes.SingleAsync(v => v.Id == hpvId);
            vaccineType.IsActive = false;
            await publicDb.SaveChangesAsync();
        }

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        var stillThere = Assert.Single(list);
        Assert.Equal("HPV", stillThere.VaccineName);
        Assert.Equal(hpvId, stillThere.VaccineTypeId);
    }

    /// <summary>Feature 013h User Story 3 (spec.md, tasks.md T042): the same guarantee, now
    /// re-verified through the real platform-admin /deactivate endpoint rather than a direct DB
    /// write — an existing VaccineRecord referencing a now-deactivated entry keeps the same
    /// fields, same read path, no error (013g FR-010, re-verified here, not re-implemented).</summary>
    [Fact]
    public async Task CreateVaccineRecord_WithVaccineTypeId_SurvivesDeactivation_ViaPlatformAdminEndpoint()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var mmrId = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken)))
            .Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!.Single(v => v.Name == "MenB").Id;

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("MenB", null, new DateOnly(2026, 6, 1), null, null, null, mmrId)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;

        var (_, adminAccessToken) = await RegisterPlatformAdminAsync(client, factory.Services);
        var deactivateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/vaccine-types/{mmrId}/deactivate", adminAccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        var stillThere = Assert.Single(list);
        Assert.Equal(record.Id, stillThere.Id);
        Assert.Equal("MenB", stillThere.VaccineName);
        Assert.Equal(mmrId, stillThere.VaccineTypeId);
    }
}
