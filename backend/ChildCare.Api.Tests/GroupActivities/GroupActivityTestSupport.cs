using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.GroupActivities;

/// <summary>Shared HTTP helpers for feature 009b's group-activities test suite — mirrors
/// ChildEventTestSupport/KioskModeTestSupport's existing conventions.</summary>
internal static class GroupActivityTestSupport
{
    public static Task<HttpResponseMessage> CreateGroupActivityAsync(
        HttpClient client, string deviceToken, string activityType, string title,
        string? description = null, DateTime? occurredAt = null, Guid? id = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/group-activities/", deviceToken, new
        {
            id,
            activityType,
            title,
            description,
            occurredAt = occurredAt ?? DateTime.UtcNow,
        }));

    public static async Task<GroupActivityResponse> CreateGroupActivityOkAsync(
        HttpClient client, string deviceToken, string activityType, string title,
        string? description = null, DateTime? occurredAt = null, Guid? id = null)
    {
        var response = await CreateGroupActivityAsync(client, deviceToken, activityType, title, description, occurredAt, id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroupActivityResponse>())!;
    }

    /// <summary>Generates a real (tiny) JPEG in-memory so the resize/thumbnail pipeline has
    /// actual image bytes to decode — a hand-rolled byte array risks not round-tripping through
    /// ImageSharp's decoder.</summary>
    public static byte[] MakeTestJpegBytes(int width = 100, int height = 100)
    {
        using var image = new Image<Rgba32>(width, height, Color.CornflowerBlue.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }

    public static async Task<HttpResponseMessage> UploadPhotoAsync(
        HttpClient client, string deviceToken, Guid activityId, byte[]? imageBytes = null, string? caption = null)
    {
        // Must await SendAsync before `content` is disposed — returning the Task directly (the
        // pattern every other helper in this file uses) would dispose the multipart content out
        // from under the in-flight async request.
        using var content = new MultipartFormDataContent();
        var photoContent = new ByteArrayContent(imageBytes ?? MakeTestJpegBytes());
        photoContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(photoContent, "photo", "photo.jpg");
        if (caption is not null)
            content.Add(new StringContent(caption), "caption");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/group-activities/{activityId}/photos")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        return await client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetTimelineAsync(HttpClient client, string deviceToken, DateOnly? date = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Get, $"/api/group-activities/timeline" + (date is null ? "" : $"?date={date:yyyy-MM-dd}"), deviceToken));

    public static Task<HttpResponseMessage> GetDirectorTimelineAsync(HttpClient client, string accessToken, Guid groupId, DateOnly date) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/group-activities/director-timeline?groupId={groupId}&date={date:yyyy-MM-dd}", accessToken));

    public static Task<HttpResponseMessage> DeleteAsDirectorAsync(HttpClient client, string accessToken, Guid id) =>
        client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/group-activities/{id}", accessToken));

    public static Task<HttpResponseMessage> GetGalleryAsync(HttpClient client, string parentToken, int? year = null, int? month = null)
    {
        var query = (year, month) switch
        {
            (not null, not null) => $"?year={year}&month={month}",
            _ => "",
        };
        return client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/group-activities/gallery{query}", parentToken));
    }

    public static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId, DateOnly startDate) =>
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, startDate)))).StatusCode);

    /// <summary>Creates and activates a contract for a child at a location, with the given
    /// photos_internal consent — the setup every consent-gating test needs.</summary>
    public static async Task<ContractResponse> CreateActiveContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, bool photosInternal, DateOnly? startDate = null)
    {
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken,
            new CreateContractRequest(
                locationId,
                startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
                null,
                [
                    new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                    new ContractedDayRequest(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                    new ContractedDayRequest(DayOfWeek.Wednesday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                    new ContractedDayRequest(DayOfWeek.Thursday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                    new ContractedDayRequest(DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                ],
                5000,
                new ContractConsentRequest(photosInternal, false, false, false, false))));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        return contract;
    }
}
