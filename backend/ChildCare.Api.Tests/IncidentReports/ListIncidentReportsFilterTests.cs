using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.IncidentReports.IncidentReportTestSupport;

namespace ChildCare.Api.Tests.IncidentReports;

/// <summary>User Story 2 (T027-T029): director cross-KDV inspection view.</summary>
public class ListIncidentReportsFilterTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task List_AsDirector_ReturnsEveryReportAcrossLocations_NewestFirst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", locationA.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", locationB.Id);
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");
        var (_, deviceA) = await PairDeviceAsync(client, org.AccessToken, locationA.Id, groupA.Id);
        var (_, deviceB) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id);

        var earlier = await FileIncidentReportAsync(client, deviceA, childA.Id, occurredAt: DateTime.UtcNow.AddHours(-2));
        var later = await FileIncidentReportAsync(client, deviceB, childB.Id, occurredAt: DateTime.UtcNow.AddHours(-1));
        var earlierBody = (await earlier.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        var laterBody = (await later.Content.ReadFromJsonAsync<IncidentReportResponse>())!;

        var response = await ListIncidentReportsAsync(client, org.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<PagedIncidentReportsResponse>())!;

        var ids = page.Items.Select(i => i.Id).ToList();
        Assert.Contains(earlierBody.Id, ids);
        Assert.Contains(laterBody.Id, ids);
        Assert.True(ids.IndexOf(laterBody.Id) < ids.IndexOf(earlierBody.Id));
    }

    [Fact]
    public async Task List_WithChildLocationAndDateFilters_NarrowsResults()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", locationA.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", locationB.Id);
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");
        var (_, deviceA) = await PairDeviceAsync(client, org.AccessToken, locationA.Id, groupA.Id);
        var (_, deviceB) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id);

        var reportA = (await (await FileIncidentReportAsync(client, deviceA, childA.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        var reportB = (await (await FileIncidentReportAsync(client, deviceB, childB.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;

        var byChild = await ListIncidentReportsAsync(client, org.AccessToken, childId: childA.Id);
        var byChildPage = (await byChild.Content.ReadFromJsonAsync<PagedIncidentReportsResponse>())!;
        Assert.All(byChildPage.Items, i => Assert.Equal(childA.Id, i.ChildId));

        var byLocation = await ListIncidentReportsAsync(client, org.AccessToken, locationId: locationB.Id);
        var byLocationPage = (await byLocation.Content.ReadFromJsonAsync<PagedIncidentReportsResponse>())!;
        Assert.All(byLocationPage.Items, i => Assert.Equal(locationB.Id, i.LocationId));
        Assert.Contains(byLocationPage.Items, i => i.Id == reportB.Id);
        Assert.DoesNotContain(byLocationPage.Items, i => i.Id == reportA.Id);

        var byDateRange = await ListIncidentReportsAsync(
            client, org.AccessToken, from: DateTime.UtcNow.AddDays(-1), to: DateTime.UtcNow.AddDays(1));
        var byDateRangePage = (await byDateRange.Content.ReadFromJsonAsync<PagedIncidentReportsResponse>())!;
        Assert.Contains(byDateRangePage.Items, i => i.Id == reportA.Id);
        Assert.Contains(byDateRangePage.Items, i => i.Id == reportB.Id);
    }

    [Fact]
    public async Task List_AsDeviceToken_IsRejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await client.SendAsync(DeviceRequest(HttpMethod.Get, "/api/incident-reports", deviceToken));
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }
}
