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
}
