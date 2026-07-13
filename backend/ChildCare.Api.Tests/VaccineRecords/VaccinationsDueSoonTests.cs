using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 3 (spec.md FR-009/FR-010): the due-soon dashboard query returns only
/// children within the 30-day window (inclusive of overdue), correctly excludes a 60-day-out
/// record, and sorts soonest/most-overdue first.</summary>
public class VaccinationsDueSoonTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task AssignToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, DateOnly.FromDateTime(DateTime.UtcNow))));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> CreateVaccineRecordAsync(
        HttpClient client, string accessToken, Guid childId, string vaccineName, DateOnly administeredOn, DateOnly? nextDueDate) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/vaccine-records", accessToken,
            new CreateVaccineRecordRequest(vaccineName, null, administeredOn, nextDueDate, null, null)));

    [Fact]
    public async Task DueSoon_ReturnsOnlyWithin30Days_SortedSoonestOrMostOverdueFirst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Due Soon Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var withinWindow = await CreateChildAsync(client, org.AccessToken, "WithinWindow");
        await AssignToGroupAsync(client, org.AccessToken, withinWindow.Id, group.Id);
        Assert.Equal(HttpStatusCode.Created, (await CreateVaccineRecordAsync(
            client, org.AccessToken, withinWindow.Id, "MMR", today.AddDays(-90), today.AddDays(10))).StatusCode);

        var overdue = await CreateChildAsync(client, org.AccessToken, "Overdue");
        await AssignToGroupAsync(client, org.AccessToken, overdue.Id, group.Id);
        Assert.Equal(HttpStatusCode.Created, (await CreateVaccineRecordAsync(
            client, org.AccessToken, overdue.Id, "Hep B", today.AddDays(-100), today.AddDays(-5))).StatusCode);

        var beyondWindow = await CreateChildAsync(client, org.AccessToken, "BeyondWindow");
        await AssignToGroupAsync(client, org.AccessToken, beyondWindow.Id, group.Id);
        Assert.Equal(HttpStatusCode.Created, (await CreateVaccineRecordAsync(
            client, org.AccessToken, beyondWindow.Id, "DTP", today.AddDays(-30), today.AddDays(60))).StatusCode);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-records/due-soon", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<List<VaccinationsDueSoonResponse>>())!;

        Assert.Equal(2, list.Count);
        Assert.Equal(overdue.Id, list[0].ChildId); // most overdue first
        Assert.True(list[0].IsOverdue);
        Assert.Equal(withinWindow.Id, list[1].ChildId);
        Assert.False(list[1].IsOverdue);
        Assert.DoesNotContain(list, x => x.ChildId == beyondWindow.Id);
    }
}
