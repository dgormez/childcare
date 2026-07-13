using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>
/// Feature 013c FR-017/SC-005 (speckit-analyze finding E1): deactivating a child leaves its
/// vaccine and health records fully queryable afterward (no cascade delete, no deactivation
/// guard registered by this feature, by design — spec.md Edge Cases). Also exercises FR-012's
/// "never auto-dismiss" — a vaccine already overdue at deactivation time is still returned by
/// the due-soon query afterward, since there is no dismiss code path for deactivation to trigger.
/// </summary>
public class RecordRetentionAfterDeactivationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task DeactivatingChild_RetainsVaccineAndHealthRecords_OverdueVaccineStillDueSoon()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Retention Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(group.Id, DateOnly.FromDateTime(DateTime.UtcNow))));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("Hep B", null, today.AddDays(-100), today.AddDays(-5), null, null)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Peanut allergy", "Confirmed by allergist.", null, null)));

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var vaccineListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, vaccineListResponse.StatusCode);
        var vaccineList = (await vaccineListResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        Assert.Single(vaccineList);

        var healthListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-records", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, healthListResponse.StatusCode);
        var healthList = (await healthListResponse.Content.ReadFromJsonAsync<List<HealthRecordResponse>>())!;
        Assert.Single(healthList);

        var dueSoonResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-records/due-soon", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, dueSoonResponse.StatusCode);
        var dueSoon = (await dueSoonResponse.Content.ReadFromJsonAsync<List<VaccinationsDueSoonResponse>>())!;
        Assert.Contains(dueSoon, x => x.ChildId == child.Id && x.IsOverdue);
    }
}
