using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.GroupActivities;

/// <summary>User Story 1 (spec.md FR-003/FR-004/FR-005) — photo upload, resize, limits.</summary>
public class GroupActivityPhotoUploadTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, string DeviceToken, Guid ActivityId)> SetupActivityAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Photo Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        return (client, deviceToken, activity.Id);
    }

    [Fact]
    public async Task UploadPhoto_ResizesAndGeneratesThumbnail()
    {
        var (client, deviceToken, activityId) = await SetupActivityAsync();

        var response = await UploadPhotoAsync(client, deviceToken, activityId, MakeTestJpegBytes(3000, 2000), "Zonnig weer");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var photo = (await response.Content.ReadFromJsonAsync<GroupActivityPhotoResponse>())!;
        Assert.NotNull(photo.DownloadUrl);
        Assert.NotNull(photo.ThumbnailDownloadUrl);
        Assert.Equal("Zonnig weer", photo.Caption);
    }

    [Fact]
    public async Task UploadPhoto_EleventhPhoto_ReturnsConflict()
    {
        var (client, deviceToken, activityId) = await SetupActivityAsync();

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Created, (await UploadPhotoAsync(client, deviceToken, activityId)).StatusCode);

        var eleventh = await UploadPhotoAsync(client, deviceToken, activityId);

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_TooLarge_ReturnsPayloadTooLarge()
    {
        var (client, deviceToken, activityId) = await SetupActivityAsync();
        var oversized = new byte[11 * 1024 * 1024];

        var response = await UploadPhotoAsync(client, deviceToken, activityId, oversized);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_UnknownActivity_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Photo Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await UploadPhotoAsync(client, deviceToken, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
