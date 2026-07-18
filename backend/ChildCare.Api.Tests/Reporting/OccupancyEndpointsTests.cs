using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 1 (spec.md FR-001/FR-002/FR-003/FR-013/FR-015/FR-016): today's
/// colour-coded occupancy per group/location, the week-ahead projection, tenant isolation
/// (including a foreign-tenant locationId), and a clean zero-present state.</summary>
public class OccupancyEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Today = ChildCare.Application.Common.BelgianCalendarDay.Today();

    private static Task<HttpResponseMessage> GetOccupancyAsync(HttpClient client, string accessToken, Guid? locationId = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, locationId is null ? "/api/reports/occupancy" : $"/api/reports/occupancy?locationId={locationId}", accessToken));

    private static async Task SetGroupCapacityAsync(HttpClient client, string accessToken, Guid groupId, int? capacity)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/groups/{groupId}/capacity", accessToken, new UpdateGroupCapacityRequest(capacity)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Occupancy_ColourCodesGroupAndLocation_AgainstCapacity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Babies", location.Id);
        await SetGroupCapacityAsync(client, org.AccessToken, group.Id, 2);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        // 2 present against a capacity of 2 -> amber (at capacity).
        var child1 = await CreateChildAsync(client, org.AccessToken, "Child1");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Child2");
        await CheckInChildAsync(client, deviceToken, child1.Id, Today);
        await CheckInChildAsync(client, deviceToken, child2.Id, Today);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child1.Id}/groups", org.AccessToken, new AssignChildToGroupRequest(group.Id, Today)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child2.Id}/groups", org.AccessToken, new AssignChildToGroupRequest(group.Id, Today)));

        var response = await GetOccupancyAsync(client, org.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<OccupancySummaryResponse>())!;

        var locationSummary = Assert.Single(body.Locations, l => l.LocationId == location.Id);
        var groupSummary = Assert.Single(locationSummary.Groups, g => g.GroupId == group.Id);
        Assert.Equal(2, groupSummary.PresentCount);
        Assert.Equal(2, groupSummary.Capacity);
        Assert.Equal("amber", groupSummary.Status);
        Assert.Equal(2, locationSummary.PresentCount);
        Assert.Equal(15, locationSummary.Capacity);
        Assert.Equal("green", locationSummary.Status);

        // FR-003: week-ahead projection is present and spans 7 days.
        Assert.Equal(7, locationSummary.WeekAhead.Count);
    }

    [Fact]
    public async Task Occupancy_GroupWithNoCapacitySet_ShowsHeadcountOnly_NoDivideByZero()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "NoCapacityGroup", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await CheckInChildAsync(client, deviceToken, child.Id, Today);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken, new AssignChildToGroupRequest(group.Id, Today)));

        var response = await GetOccupancyAsync(client, org.AccessToken);
        var body = (await response.Content.ReadFromJsonAsync<OccupancySummaryResponse>())!;
        var groupSummary = Assert.Single(body.Locations.Single(l => l.LocationId == location.Id).Groups, g => g.GroupId == group.Id);

        Assert.Equal(1, groupSummary.PresentCount);
        Assert.Null(groupSummary.Capacity);
        Assert.Null(groupSummary.Status);
    }

    [Fact]
    public async Task Occupancy_NoChildrenPresent_ShowsCleanZeroOverCapacity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Empty Location");

        var response = await GetOccupancyAsync(client, org.AccessToken, location.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<OccupancySummaryResponse>())!;
        var locationSummary = Assert.Single(body.Locations);
        Assert.Equal(0, locationSummary.PresentCount);
        Assert.Equal(15, locationSummary.Capacity);
        Assert.Equal("green", locationSummary.Status);
    }

    [Fact]
    public async Task Occupancy_CrossTenant_NeverLeaksOtherTenantsData()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Tenant A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, orgA.AccessToken, "Location A");

        var orgB = await RegisterOrgAsync(client, $"Tenant B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var locationB = await CreateLocationAsync(client, orgB.AccessToken, "Location B");

        // Tenant B's aggregate view never includes tenant A's location.
        var responseB = await GetOccupancyAsync(client, orgB.AccessToken);
        var bodyB = (await responseB.Content.ReadFromJsonAsync<OccupancySummaryResponse>())!;
        Assert.DoesNotContain(bodyB.Locations, l => l.LocationId == locationA.Id);

        // A locationId belonging to another tenant is treated as no valid selection, not leaked.
        var crossTenantResponse = await GetOccupancyAsync(client, orgB.AccessToken, locationA.Id);
        Assert.Equal(HttpStatusCode.OK, crossTenantResponse.StatusCode);
        var crossTenantBody = (await crossTenantResponse.Content.ReadFromJsonAsync<OccupancySummaryResponse>())!;
        Assert.Empty(crossTenantBody.Locations);

        _ = locationB;
    }
}
