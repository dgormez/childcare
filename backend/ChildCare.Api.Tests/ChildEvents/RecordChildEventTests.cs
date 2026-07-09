using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 1 (T011/T012a): routine event recording happy path and create idempotency.</summary>
public class RecordChildEventTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Theory]
    [InlineData("diaper")]
    [InlineData("feeding_bottle")]
    [InlineData("mood")]
    public async Task RecordEvent_HappyPath_PersistsAndReturns201(string eventType)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"RecordEvent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        object payload = eventType switch
        {
            "diaper" => new { type = "wet" },
            "feeding_bottle" => new { ml = 120 },
            "mood" => new { value = "good" },
            _ => throw new InvalidOperationException(),
        };

        var response = await PostChildEventAsync(client, deviceToken, child.Id, eventType, DateTime.UtcNow, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Equal(child.Id, body.ChildId);
        Assert.Equal(eventType, body.EventType);
        Assert.True(body.VisibleToParent);
    }

    [Fact]
    public async Task RecordEvent_Retried_WithSameClientGeneratedId_ReturnsExistingRecord_NotDuplicate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Idempotent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var clientGeneratedId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var first = await PostChildEventAsync(client, deviceToken, child.Id, "diaper", occurredAt, new { type = "wet" }, id: clientGeneratedId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var retry = await PostChildEventAsync(client, deviceToken, child.Id, "diaper", occurredAt, new { type = "wet" }, id: clientGeneratedId);
        var retryBody = (await retry.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Equal(firstBody.Id, retryBody.Id);

        var list = await GetChildEventsAsync(client, deviceToken, child.Id);
        var events = (await list.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        Assert.Single(events.Items, e => e.Id == clientGeneratedId);
    }
}
