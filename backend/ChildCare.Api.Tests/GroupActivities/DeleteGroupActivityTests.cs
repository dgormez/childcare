using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.GroupActivities;

/// <summary>User Story 4 (spec.md FR-011) — director deletion removes an activity from every surface.</summary>
public class DeleteGroupActivityTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Delete_RemovesActivityAndPhotos_204()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Delete Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "celebration", "Verjaardag");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var deleteResponse = await DeleteAsDirectorAsync(client, org.AccessToken, activity.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var secondDelete = await DeleteAsDirectorAsync(client, org.AccessToken, activity.Id);
        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesFromCaregiverTimelineDailySummaryAndGallery()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Delete Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: true);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "celebration", "Verjaardag");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsDirectorAsync(client, org.AccessToken, activity.Id)).StatusCode);

        var timeline = (await (await GetTimelineAsync(client, deviceToken)).Content.ReadFromJsonAsync<GroupTimelineResponse>())!;
        Assert.DoesNotContain(timeline.Entries, e => e.Kind == "group_activity" && e.GroupActivity!.Id == activity.Id);

        var summaryResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/parent/children/{child.Id}/daily-summary?date={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}", parentToken));
        var summary = (await summaryResponse.Content.ReadFromJsonAsync<DailySummaryResponse>())!;
        Assert.Empty(summary.GroupActivities);

        var gallery = (await (await GetGalleryAsync(client, parentToken)).Content.ReadFromJsonAsync<GalleryResponse>())!;
        Assert.DoesNotContain(gallery.Items, i => i.ActivityId == activity.Id);
    }

    [Fact]
    public async Task DirectorTimeline_AfterDelete_NoLongerShowsActivity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Delete Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "story", "Verhaaltje");
        await DeleteAsDirectorAsync(client, org.AccessToken, activity.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var timeline = (await (await GetDirectorTimelineAsync(client, org.AccessToken, group.Id, today)).Content.ReadFromJsonAsync<GroupTimelineResponse>())!;

        Assert.DoesNotContain(timeline.Entries, e => e.Kind == "group_activity" && e.GroupActivity!.Id == activity.Id);
    }
}
