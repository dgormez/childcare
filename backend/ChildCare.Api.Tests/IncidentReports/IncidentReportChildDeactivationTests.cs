using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.IncidentReports.IncidentReportTestSupport;

namespace ChildCare.Api.Tests.IncidentReports;

/// <summary>User Story 3 (T048): incident reports survive their child's deactivation (FR-008).</summary>
public class IncidentReportChildDeactivationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Report_RemainsRetrievable_AfterChildIsDeactivated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var filed = (await (await FileIncidentReportAsync(client, deviceToken, child.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var detail = await GetIncidentReportAsDirectorAsync(client, org.AccessToken, filed.Id);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var list = await ListIncidentReportsAsync(client, org.AccessToken, childId: child.Id);
        var page = (await list.Content.ReadFromJsonAsync<PagedIncidentReportsResponse>())!;
        Assert.Contains(page.Items, i => i.Id == filed.Id);
    }
}
