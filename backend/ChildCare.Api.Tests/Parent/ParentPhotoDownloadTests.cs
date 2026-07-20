using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.Parent;

/// <summary>User Story 2 (031-photo-lifecycle-governance, spec.md FR-012/FR-013/FR-014): a
/// parent can download the full-resolution original of their own child's profile photo, or a
/// group-activity photo their child is derived as depicted in — never a photo they have no
/// ownership/consent basis to see.</summary>
public class ParentPhotoDownloadTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> DownloadAsync(HttpClient client, string parentToken, string photoType, Guid objectRef) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/photos/{photoType}/{objectRef}/download", parentToken));

    [Fact]
    public async Task DownloadProfilePhoto_OwnChild_ReturnsFullResolutionAttachmentUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/photo/upload-url", org.AccessToken));

        var response = await DownloadAsync(client, parentToken, "profile", child.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ParentPhotoDownloadResponse>())!;

        Assert.Contains($"children/{child.Id}/photo.jpg", body.DownloadUrl);
        Assert.Contains("attachment=", body.DownloadUrl);
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task DownloadProfilePhoto_NotOwnChild_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var otherChild = await CreateChildAsync(client, org.AccessToken, "Noah");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{otherChild.Id}/photo/upload-url", org.AccessToken));

        var response = await DownloadAsync(client, parentToken, "profile", otherChild.Id);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.photos.forbidden", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DownloadProfilePhoto_NoPhotoSet_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await DownloadAsync(client, parentToken, "profile", child.Id);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.photos.not_found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DownloadGroupActivityPhoto_OwnChildDepicted_ReturnsFullResolutionAttachmentUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: true);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var galleryResponse = await GetGalleryAsync(client, parentToken);
        var gallery = (await galleryResponse.Content.ReadFromJsonAsync<GalleryResponse>())!;
        var photo = Assert.Single(gallery.Items).Photo;

        var response = await DownloadAsync(client, parentToken, "group-activity", photo.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ParentPhotoDownloadResponse>())!;

        Assert.Contains($"group-activities/{activity.Id}/", body.DownloadUrl);
        Assert.DoesNotContain("-thumb", body.DownloadUrl); // full-resolution object, never the thumbnail
        Assert.Contains("attachment=", body.DownloadUrl);
    }

    [Fact]
    public async Task DownloadGroupActivityPhoto_NoConsent_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: false);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        var uploaded = await UploadPhotoAsync(client, deviceToken, activity.Id);
        var photo = (await uploaded.Content.ReadFromJsonAsync<GroupActivityPhotoResponse>())!;

        var response = await DownloadAsync(client, parentToken, "group-activity", photo.Id);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.photos.forbidden", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DownloadGroupActivityPhoto_ChildNotDepicted_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, location.Id, groupB.Id);

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, groupA.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, photosInternal: true);

        var activity = await CreateGroupActivityOkAsync(client, deviceTokenB, "outdoor", "Group B activity");
        var uploaded = await UploadPhotoAsync(client, deviceTokenB, activity.Id);
        var photo = (await uploaded.Content.ReadFromJsonAsync<GroupActivityPhotoResponse>())!;

        var response = await DownloadAsync(client, parentToken, "group-activity", photo.Id);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DownloadUnknownPhotoType_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await DownloadAsync(client, parentToken, "health-attachment", child.Id);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
