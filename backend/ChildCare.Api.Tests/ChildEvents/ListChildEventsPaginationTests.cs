using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 1 (T014): cursor pagination (research.md R6) — no gaps or overlap across pages.</summary>
public class ListChildEventsPaginationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task ListEvents_25Events_PaginatesInPagesOf10_NoGapsOrOverlap()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Pagination Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var baseTime = DateTime.UtcNow.AddHours(-2);
        var createdIds = new List<Guid>();
        for (var i = 0; i < 25; i++)
        {
            var response = await PostChildEventAsync(
                client, deviceToken, child.Id, "note", baseTime.AddMinutes(i), new { text = $"note {i}" });
            var body = (await response.Content.ReadFromJsonAsync<ChildEventResponse>())!;
            createdIds.Add(body.Id);
        }

        var seenIds = new List<Guid>();
        string? cursor = null;
        for (var page = 0; page < 10; page++)
        {
            var response = await GetChildEventsAsync(client, deviceToken, child.Id, before: cursor, limit: 10);
            var body = (await response.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
            seenIds.AddRange(body.Items.Select(e => e.Id));

            if (body.NextCursor is null)
                break;
            cursor = body.NextCursor;
        }

        // Most-recent-first ordering means the 25 created ids, reversed, is the expected order.
        Assert.Equal(createdIds.AsEnumerable().Reverse(), seenIds);
        Assert.Equal(seenIds.Count, seenIds.Distinct().Count());
    }
}
