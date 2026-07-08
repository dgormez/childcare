using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>Feature 007a (spec.md FR-013a): the director web Devices screen needs a list
/// endpoint feature 008a never built (only pair/revoke/exit-room-mode). Happy path plus tenant
/// isolation and the empty-tenant case, per this project's testing convention.</summary>
public class DeviceListingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task ListDevices_ReturnsResolvedNamesAndRevokedStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Devices List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (activeDeviceId, _) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id, "111111");
        var (revokedDeviceId, _) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id, "222222");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/devices/{revokedDeviceId}/revoke", org.AccessToken));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/devices", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var devices = (await response.Content.ReadFromJsonAsync<List<DeviceSummaryResponse>>())!;

        var active = Assert.Single(devices, d => d.Id == activeDeviceId);
        Assert.Equal(location.Id, active.LocationId);
        Assert.Equal("Location A", active.LocationName);
        Assert.Equal(group.Id, active.GroupId);
        Assert.Equal("Group A", active.GroupName);
        Assert.Equal(org.Director.Id, active.PairedByTenantUserId);
        Assert.Equal(org.Director.Name, active.PairedByName);
        Assert.Null(active.RevokedAt);

        var revoked = Assert.Single(devices, d => d.Id == revokedDeviceId);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task ListDevices_EmptyTenant_Returns200EmptyArray()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Devices Empty Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/devices", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var devices = (await response.Content.ReadFromJsonAsync<List<DeviceSummaryResponse>>())!;
        Assert.Empty(devices);
    }

    [Fact]
    public async Task ListDevices_NeverReturnsAnotherTenantsDevices()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Devices Tenant A {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, orgA.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, orgA.AccessToken, "Group A", locationA.Id);
        await PairDeviceAsync(client, orgA.AccessToken, locationA.Id, groupA.Id, "333333");

        var orgB = await RegisterOrgAsync(client, $"Devices Tenant B {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/devices", orgB.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var devices = (await response.Content.ReadFromJsonAsync<List<DeviceSummaryResponse>>())!;
        Assert.Empty(devices);
    }
}
