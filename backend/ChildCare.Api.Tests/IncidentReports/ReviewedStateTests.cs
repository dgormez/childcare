using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.IncidentReports.IncidentReportTestSupport;

namespace ChildCare.Api.Tests.IncidentReports;

/// <summary>User Story 2 (T030): opening a report's detail view marks it reviewed (FR-010/FR-011).</summary>
public class ReviewedStateTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetDetail_AsDirector_SetsReviewedAtOnFirstOpen_LeavesUnchangedOnSubsequentOpens()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var filed = (await (await FileIncidentReportAsync(client, deviceToken, child.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Null(filed.ReviewedAt);

        var firstOpen = await GetIncidentReportAsDirectorAsync(client, org.AccessToken, filed.Id);
        Assert.Equal(HttpStatusCode.OK, firstOpen.StatusCode);
        var firstBody = (await firstOpen.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.NotNull(firstBody.ReviewedAt);

        await Task.Delay(50);
        var secondOpen = await GetIncidentReportAsDirectorAsync(client, org.AccessToken, filed.Id);
        var secondBody = (await secondOpen.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Equal(firstBody.ReviewedAt, secondBody.ReviewedAt);
    }
}
