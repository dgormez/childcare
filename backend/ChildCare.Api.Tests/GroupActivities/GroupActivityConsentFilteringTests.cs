using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.GroupActivities;

/// <summary>User Story 2 (spec.md FR-008/FR-009) + User Story 3 (FR-010) — daily-feed and
/// gallery consent filtering (research.md R5/R6).</summary>
public class GroupActivityConsentFilteringTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> GetParentDailySummaryAsync(HttpClient client, string parentToken, Guid childId, DateOnly date) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{childId}/daily-summary?date={date:yyyy-MM-dd}", parentToken));

    [Fact]
    public async Task DailySummary_ConsentTrue_PhotosPopulated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Consent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: true);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var response = await GetParentDailySummaryAsync(client, parentToken, child.Id, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;

        var groupActivity = Assert.Single(summary.GroupActivities);
        Assert.Equal("In de tuin", groupActivity.Title);
        Assert.Single(groupActivity.Photos);
    }

    [Fact]
    public async Task DailySummary_NoConsent_PhotosEmpty_TextStillPresent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Consent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: false);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var response = await GetParentDailySummaryAsync(client, parentToken, child.Id, DateOnly.FromDateTime(DateTime.UtcNow));
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;

        var groupActivity = Assert.Single(summary.GroupActivities);
        Assert.Equal("In de tuin", groupActivity.Title);
        Assert.Empty(groupActivity.Photos);
    }

    [Fact]
    public async Task DailySummary_ChildNotInGroupThatDay_ActivityNotShown()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Consent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, location.Id, groupB.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        // Child belongs to Group A, not Group B.
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, groupA.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: true);

        // Activity recorded for Group B today — should not appear for a Group A child.
        await CreateGroupActivityOkAsync(client, deviceTokenB, "outdoor", "Group B activity");

        var response = await GetParentDailySummaryAsync(client, parentToken, child.Id, DateOnly.FromDateTime(DateTime.UtcNow));
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;

        Assert.Empty(summary.GroupActivities);
    }

    [Fact]
    public async Task Gallery_AggregatesAcrossGroups_DedupsTwinsInSameGroup()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Gallery Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child1.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child1.Id, location.Id, photosInternal: true);

        // Twin: a second child in the same group, linked to the same parent contact.
        var child2 = await CreateChildAsync(client, org.AccessToken, "Liam");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, child2.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child2.Id, location.Id, photosInternal: true);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        await UploadPhotoAsync(client, deviceToken, activity.Id);
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var response = await GetGalleryAsync(client, parentToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var gallery = (await response.Content.ReadFromJsonAsync<GalleryResponse>())!;

        Assert.True(gallery.HasConsent);
        // 2 photos on the one shared activity — not 4 (would be 4 if duplicated per twin).
        Assert.Equal(2, gallery.Items.Count);
        Assert.All(gallery.Items, i => Assert.Equal(activity.Id, i.ActivityId));
    }

    [Fact]
    public async Task Gallery_NoConsent_ReturnsEmptyWithHasConsentFalse()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Gallery Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: false);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var response = await GetGalleryAsync(client, parentToken);
        var gallery = (await response.Content.ReadFromJsonAsync<GalleryResponse>())!;

        Assert.False(gallery.HasConsent);
        Assert.Empty(gallery.Items);
    }

    [Fact]
    public async Task Gallery_TextOnlyActivity_ExcludedFromResults()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Gallery Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: true);

        // No photo attached — text-only activity.
        await CreateGroupActivityOkAsync(client, deviceToken, "other", "Text only, no photos");

        var response = await GetGalleryAsync(client, parentToken);
        var gallery = (await response.Content.ReadFromJsonAsync<GalleryResponse>())!;

        Assert.True(gallery.HasConsent);
        Assert.Empty(gallery.Items);
    }
}
