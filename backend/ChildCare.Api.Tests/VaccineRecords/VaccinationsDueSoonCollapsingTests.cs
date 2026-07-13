using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 3: an empty result set is a 200 with an empty array, and a child with
/// multiple due-soon vaccines appears once, showing its most urgent one (research.md R4).</summary>
public class VaccinationsDueSoonCollapsingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> CreateVaccineRecordAsync(
        HttpClient client, string accessToken, Guid childId, string vaccineName, DateOnly administeredOn, DateOnly? nextDueDate) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/vaccine-records", accessToken,
            new CreateVaccineRecordRequest(vaccineName, null, administeredOn, nextDueDate, null, null)));

    [Fact]
    public async Task DueSoon_NoUpcomingVaccines_ReturnsEmptyArray()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Due Soon Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-records/due-soon", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<List<VaccinationsDueSoonResponse>>())!;
        Assert.Empty(list);
    }

    [Fact]
    public async Task DueSoon_ChildWithMultipleDueSoonVaccines_AppearsOnceWithMostUrgent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Due Soon Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var child = await CreateChildAsync(client, org.AccessToken, "MultiVaccine");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(group.Id, today)));

        await CreateVaccineRecordAsync(client, org.AccessToken, child.Id, "MMR", today.AddDays(-60), today.AddDays(20));
        await CreateVaccineRecordAsync(client, org.AccessToken, child.Id, "DTP", today.AddDays(-60), today.AddDays(3));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-records/due-soon", org.AccessToken));
        var list = (await response.Content.ReadFromJsonAsync<List<VaccinationsDueSoonResponse>>())!;

        var childRows = list.Where(x => x.ChildId == child.Id).ToList();
        Assert.Single(childRows);
        Assert.Equal("DTP", childRows[0].VaccineName); // most urgent (soonest) of the two
    }
}
