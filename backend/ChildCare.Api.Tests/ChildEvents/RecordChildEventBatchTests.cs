using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>Feature 009c — contracts/child-events-batch-api.md. User Stories 1 and 2.</summary>
public class RecordChildEventBatchTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, ChildCare.Contracts.Responses.LocationResponse Location, GroupResponse Group, string DeviceToken, RegisterOrganisationResponse Org)>
        SetupRoomAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Batch Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        return (client, location, group, deviceToken, org);
    }

    private static async Task<ChildResponse> CreatePresentChildAsync(HttpClient client, string accessToken, string deviceToken, string firstName = "Emma")
    {
        var child = await CreateChildAsync(client, accessToken, firstName);
        await CheckInChildAsync(client, deviceToken, child.Id, BelgianCalendarDay());
        return child;
    }

    // BelgianCalendarDay.Today() lives in ChildCare.Application, not referenceable from the API
    // test project without a project reference this suite doesn't otherwise need — UTC-today is
    // equivalent for CI purposes since TestContainers runs in UTC and Brussels/UTC only diverge
    // right at midnight, which these tests don't run near.
    private static DateOnly BelgianCalendarDay() => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Batch_AllChildrenPresent_CreatesOneEventPerChild()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var children = new List<ChildResponse>();
        for (var i = 0; i < 8; i++)
            children.Add(await CreatePresentChildAsync(client, org.AccessToken, deviceToken, $"Child{i}"));

        var occurredAt = DateTime.UtcNow;
        var response = await PostChildEventBatchAsync(
            client, deviceToken, children.Select(c => c.Id), "diaper", occurredAt, new { type = "wet" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ChildEventBatchResponse>())!;
        Assert.Equal(8, body.Created.Count);
        Assert.Empty(body.Errors);

        foreach (var child in children)
        {
            var events = await GetChildEventsAsync(client, deviceToken, child.Id);
            var page = (await events.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
            // Tolerant, not exact, equality on OccurredAt — PostgreSQL's timestamp round-trip
            // precision can differ by a few ticks from the in-memory DateTime.UtcNow value
            // (feature 010 hit this same CI-only flake: ".4072354Z vs .4072350Z").
            Assert.Single(page.Items, e => e.EventType == "diaper" && (e.OccurredAt - occurredAt).Duration() < TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task Batch_UnsupportedEventType_RejectsWholeBatchBeforeAnyRowCreated()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var child = await CreatePresentChildAsync(client, org.AccessToken, deviceToken);

        var response = await PostChildEventBatchAsync(
            client, deviceToken, [child.Id], "temperature", DateTime.UtcNow, new { celsius = 37.5m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var events = await GetChildEventsAsync(client, deviceToken, child.Id);
        var page = (await events.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Batch_MoreThan30Children_RejectsWholeBatchBeforeAnyRowCreated()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var children = new List<ChildResponse>();
        for (var i = 0; i < 31; i++)
            children.Add(await CreatePresentChildAsync(client, org.AccessToken, deviceToken, $"Child{i}"));

        var response = await PostChildEventBatchAsync(
            client, deviceToken, children.Select(c => c.Id), "note", DateTime.UtcNow, new { text = "hi" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var events = await GetChildEventsAsync(client, deviceToken, children[0].Id);
        var page = (await events.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Batch_ChildDoesNotExist_ReportedAsChildNotFound_OthersStillSucceed()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var realChild = await CreatePresentChildAsync(client, org.AccessToken, deviceToken);
        var fakeChildId = Guid.NewGuid();

        var response = await PostChildEventBatchAsync(
            client, deviceToken, [realChild.Id, fakeChildId], "note", DateTime.UtcNow, new { text = "hi" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ChildEventBatchResponse>())!;
        Assert.Single(body.Created, c => c.ChildId == realChild.Id);
        Assert.Single(body.Errors, e => e.ChildId == fakeChildId && e.Reason == "child_not_found");
    }

    [Fact]
    public async Task Batch_OneChildCheckedOut_ReportedAsNotPresent_OthersStillSucceed()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var stayingChildren = new List<ChildResponse>
        {
            await CreatePresentChildAsync(client, org.AccessToken, deviceToken, "Staying1"),
            await CreatePresentChildAsync(client, org.AccessToken, deviceToken, "Staying2"),
        };
        var checkedOutChild = await CreatePresentChildAsync(client, org.AccessToken, deviceToken, "CheckedOut");
        await CheckOutChildAsync(client, deviceToken, checkedOutChild.Id, BelgianCalendarDay());

        var allChildIds = stayingChildren.Select(c => c.Id).Append(checkedOutChild.Id);
        var response = await PostChildEventBatchAsync(client, deviceToken, allChildIds, "sleep", DateTime.UtcNow, new { quality = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ChildEventBatchResponse>())!;
        Assert.Equal(2, body.Created.Count);
        Assert.Single(body.Errors, e => e.ChildId == checkedOutChild.Id && e.Reason == "not_present");

        foreach (var staying in stayingChildren)
        {
            var events = await GetChildEventsAsync(client, deviceToken, staying.Id);
            var page = (await events.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
            Assert.Single(page.Items, e => e.EventType == "sleep");
        }
    }

    [Fact]
    public async Task Batch_EveryChildFails_ReturnsFullFailureResult_Not200WithSurpriseSuccesses()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var child = await CreateChildAsync(client, org.AccessToken); // never checked in
        var stillFakeChildId = Guid.NewGuid();

        var response = await PostChildEventBatchAsync(
            client, deviceToken, [child.Id, stillFakeChildId], "diaper", DateTime.UtcNow, new { type = "wet" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ChildEventBatchResponse>())!;
        Assert.Empty(body.Created);
        Assert.Equal(2, body.Errors.Count);
        Assert.Contains(body.Errors, e => e.ChildId == child.Id && e.Reason == "not_present");
        Assert.Contains(body.Errors, e => e.ChildId == stillFakeChildId && e.Reason == "child_not_found");
    }

    [Fact]
    public async Task Batch_RetriedWithSameClientGeneratedIds_DoesNotDuplicate()
    {
        var (client, _, _, deviceToken, org) = await SetupRoomAsync();
        var child = await CreatePresentChildAsync(client, org.AccessToken, deviceToken);
        var occurredAt = DateTime.UtcNow;
        var clientId = Guid.NewGuid();

        var items = new[] { (child.Id, clientId) };
        var first = await PostChildEventBatchAsync(client, deviceToken, items, "note", occurredAt, new { text = "hi" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ChildEventBatchResponse>())!;
        Assert.Single(firstBody.Created);

        var retry = await PostChildEventBatchAsync(client, deviceToken, items, "note", occurredAt, new { text = "hi" });
        var retryBody = (await retry.Content.ReadFromJsonAsync<ChildEventBatchResponse>())!;
        Assert.Single(retryBody.Created, c => c.EventId == firstBody.Created[0].EventId);

        var events = await GetChildEventsAsync(client, deviceToken, child.Id);
        var page = (await events.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        Assert.Single(page.Items, e => e.Id == clientId);
    }
}
