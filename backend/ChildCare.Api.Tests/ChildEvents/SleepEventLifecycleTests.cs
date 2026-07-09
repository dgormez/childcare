using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 1 (T013): sleep event in-progress → completed lifecycle (FR-004).</summary>
public class SleepEventLifecycleTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task SleepEvent_CreatedWithNoEndedAt_ShowsInProgress_ThenCompletedWithComputedDuration()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sleep Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var occurredAt = DateTime.UtcNow.AddHours(-1);
        var createResponse = await PostChildEventAsync(client, deviceToken, child.Id, "sleep", occurredAt, new { });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Null(created.EndedAt);

        var endedAt = occurredAt.AddMinutes(90);
        var patchResponse = await PatchChildEventAsDeviceAsync(client, deviceToken, created.Id, endedAt: endedAt, payload: new { quality = "good" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var completed = (await patchResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Equal(endedAt, completed.EndedAt!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(90, completed.Payload.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("good", completed.Payload.GetProperty("quality").GetString());
    }

    [Fact]
    public async Task SleepEvent_EndedWithoutQuality_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SleepNoQuality Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var occurredAt = DateTime.UtcNow.AddHours(-1);
        var createResponse = await PostChildEventAsync(client, deviceToken, child.Id, "sleep", occurredAt, new { });
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        // PATCH sets endedAt only, no payload at all — quality is now required (data-model.md)
        // even though this PATCH never touched the payload.
        var patchResponse = await PatchChildEventAsDeviceAsync(client, deviceToken, created.Id, endedAt: occurredAt.AddMinutes(30));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, patchResponse.StatusCode);
    }
}
