using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.GroupActivities;

/// <summary>User Story 1 (spec.md FR-007) + User Story 4 (director timeline) — research.md R4's
/// merged ChildEvent/GroupActivity timeline, reused by both endpoints.</summary>
public class GroupTimelineOrderingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Timeline_MergesChildEventsAndGroupActivities_ChronologicalOrder()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Timeline Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var child = await CreateChildAsync(client, org.AccessToken);

        var t0 = DateTime.UtcNow.Date.AddHours(9);
        await PostChildEventAsync(client, deviceToken, child.Id, "diaper", t0, new { type = "wet" });
        await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin", occurredAt: t0.AddMinutes(15));
        await PostChildEventAsync(client, deviceToken, child.Id, "diaper", t0.AddMinutes(30), new { type = "dirty" });

        var response = await GetTimelineAsync(client, deviceToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var timeline = (await response.Content.ReadFromJsonAsync<GroupTimelineResponse>())!;

        Assert.True(timeline.Entries.Count >= 3);
        var kinds = timeline.Entries.Select(e => e.Kind).ToList();
        Assert.Contains("child_event", kinds);
        Assert.Contains("group_activity", kinds);
        Assert.Equal(timeline.Entries.OrderBy(e => e.OccurredAt).Select(e => e.OccurredAt), timeline.Entries.Select(e => e.OccurredAt));
    }

    [Fact]
    public async Task DirectorTimeline_RequiresExplicitDate_ReturnsSameMergedShape()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Timeline Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateGroupActivityOkAsync(client, deviceToken, "story", "Verhaaltje");

        var response = await GetDirectorTimelineAsync(client, org.AccessToken, group.Id, today);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var timeline = (await response.Content.ReadFromJsonAsync<GroupTimelineResponse>())!;
        Assert.Contains(timeline.Entries, e => e.Kind == "group_activity");
    }

    [Fact]
    public async Task Timeline_GroupIdMismatch_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Timeline Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var otherGroup = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await client.SendAsync(DeviceRequest(HttpMethod.Get, $"/api/group-activities/timeline?groupId={otherGroup.Id}", deviceToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
