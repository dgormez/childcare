using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 3 (T033-T035a): same-day edit/delete authorization (FR-006/FR-007, research.md R4).</summary>
public class ChildEventEditWindowTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task SameDayEvent_EditedFromSameLocationDeviceToken_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"EditSameLoc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var createResponse = await PostChildEventAsync(client, deviceToken, child.Id, "note", DateTime.UtcNow, new { text = "original" });
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var patchResponse = await PatchChildEventAsDeviceAsync(client, deviceToken, created.Id, payload: new { text = "corrected" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var updated = (await patchResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Equal("corrected", updated.Payload.GetProperty("text").GetString());
    }

    [Fact]
    public async Task PriorDayEvent_EditedFromDeviceToken_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"EditPriorDay Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var createResponse = await PostChildEventAsync(
            client, deviceToken, child.Id, "note", DateTime.UtcNow.AddDays(-2), new { text = "yesterday" });
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var patchResponse = await PatchChildEventAsDeviceAsync(client, deviceToken, created.Id, payload: new { text = "too late" });
        Assert.Equal(HttpStatusCode.Forbidden, patchResponse.StatusCode);
        Assert.Contains("errors.child_events.edit_window_expired", await patchResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Director_CanEditAnyEvent_RegardlessOfDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DirectorEdit Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var createResponse = await PostChildEventAsync(
            client, deviceToken, child.Id, "note", DateTime.UtcNow.AddDays(-10), new { text = "old" });
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var patchResponse = await PatchChildEventAsDirectorAsync(client, org.AccessToken, created.Id, payload: new { text = "corrected by director" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
    }

    [Fact]
    public async Task SameDayEvent_EditedFromDifferentLocationDeviceToken_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"EditDiffLoc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", locationA.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", locationB.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceTokenA) = await PairDeviceAsync(client, org.AccessToken, locationA.Id, groupA.Id, "111222");
        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id, "333444");

        var createResponse = await PostChildEventAsync(client, deviceTokenA, child.Id, "note", DateTime.UtcNow, new { text = "at location A" });
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var patchFromB = await PatchChildEventAsDeviceAsync(client, deviceTokenB, created.Id, payload: new { text = "from B" });
        Assert.Equal(HttpStatusCode.Forbidden, patchFromB.StatusCode);

        var patchFromA = await PatchChildEventAsDeviceAsync(client, deviceTokenA, created.Id, payload: new { text = "from A" });
        Assert.Equal(HttpStatusCode.OK, patchFromA.StatusCode);
    }
}
