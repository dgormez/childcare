using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 1 (spec.md FR-004): live per-group BKR ratio, extending
/// `GetBkrRatioQuery`'s location-scoped rules (research.md R2) to group scope.</summary>
public class BkrRatioEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Today = ChildCare.Application.Common.BelgianCalendarDay.Today();

    private static Task<HttpResponseMessage> GetGroupBkrAsync(HttpClient client, string accessToken, Guid? locationId = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, locationId is null ? "/api/reports/bkr" : $"/api/reports/bkr?locationId={locationId}", accessToken));

    [Fact]
    public async Task GroupBkrRatio_ReflectsPresentChildrenAndQualifiedStaff_ScopedToThatGroup()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Bkr Report Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, groupA.Id);

        // Group A: 9 present, 1 qualified caregiver -> breach (threshold 8).
        var caregiverA = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111");
        await CheckInAsync(client, deviceToken, caregiverA.Id, "1111");
        for (var i = 0; i < 9; i++)
        {
            var child = await CreateChildAsync(client, org.AccessToken, $"GroupAChild{i}_{Guid.NewGuid():N}");
            await CheckInChildAsync(client, deviceToken, child.Id, Today);
            await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken, new AssignChildToGroupRequest(groupA.Id, Today)));
        }

        // Group B: no one present, no staff -> compliant (green).
        var response = await GetGroupBkrAsync(client, org.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BkrRatioOverviewResponse>())!;

        var groupASummary = Assert.Single(body.Groups, g => g.GroupId == groupA.Id);
        Assert.Equal(9, groupASummary.PresentCount);
        Assert.Equal(1, groupASummary.QualifiedStaffCount);
        Assert.Equal(8, groupASummary.Threshold);
        Assert.Equal("red", groupASummary.Status);

        var groupBSummary = Assert.Single(body.Groups, g => g.GroupId == groupB.Id);
        Assert.Equal(0, groupBSummary.PresentCount);
        Assert.Equal(0, groupBSummary.QualifiedStaffCount);
        Assert.Equal("green", groupBSummary.Status);
    }

    [Fact]
    public async Task GroupBkrRatio_CrossTenant_NeverLeaksOtherTenantsGroups()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Tenant A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, orgA.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, orgA.AccessToken, "Group A", locationA.Id);

        var orgB = await RegisterOrgAsync(client, $"Tenant B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");

        var response = await GetGroupBkrAsync(client, orgB.AccessToken);
        var body = (await response.Content.ReadFromJsonAsync<BkrRatioOverviewResponse>())!;
        Assert.DoesNotContain(body.Groups, g => g.GroupId == groupA.Id);
    }
}
