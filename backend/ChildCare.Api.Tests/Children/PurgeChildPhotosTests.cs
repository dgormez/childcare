using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.Children;

/// <summary>User Story 1 (031-photo-lifecycle-governance, spec.md FR-007/FR-008/FR-016): a
/// director or staff member purges a deactivated child's photos in one action — profile photo,
/// health/vaccine attachments, and any group-activity photo where the child is the sole depicted
/// child are deleted; a photo shared with another (still-active) child is left untouched.</summary>
public class PurgeChildPhotosTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> PurgeAsync(HttpClient client, string accessToken, Guid childId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/purge-photos", accessToken));

    private record Fixture(
        HttpClient Client, string AccessToken, Guid ChildAId, Guid ChildBId, Guid GroupId,
        Guid SoleActivityId, Guid SharedActivityId, Guid HealthRecordId, Guid VaccineRecordId);

    private static async Task<Fixture> BuildAsync(HttpClient client)
    {
        var org = await RegisterOrgAsync(client, $"Purge Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");

        // childA has belonged to the group since well before either activity below.
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));

        // Sole-depicted activity: occurs before childB ever joins the group.
        var soleActivity = await CreateGroupActivityOkAsync(
            client, deviceToken, "outdoor", "Solo walk", occurredAt: DateTime.UtcNow.AddDays(-50));
        var solePhoto = await UploadPhotoAsync(client, deviceToken, soleActivity.Id);
        Assert.Equal(HttpStatusCode.Created, solePhoto.StatusCode);

        // childB joins the same group later — subsequent activities depict both children.
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)));

        var sharedActivity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "Shared walk", occurredAt: DateTime.UtcNow);
        var sharedPhoto = await UploadPhotoAsync(client, deviceToken, sharedActivity.Id);
        Assert.Equal(HttpStatusCode.Created, sharedPhoto.StatusCode);

        // Profile photo.
        var photoUrlResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/photo/upload-url", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, photoUrlResponse.StatusCode);

        // Health record with attachment.
        var healthCreated = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        var healthRecord = (await healthCreated.Content.ReadFromJsonAsync<HealthRecordResponse>())!;
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childA.Id}/health-records/{healthRecord.Id}/attachment-upload-url", org.AccessToken,
            new CreateHealthRecordAttachmentUploadUrlRequest("application/pdf")));

        // Vaccine record with attachment.
        var vaccineCreated = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("DTP", 2, new DateOnly(2026, 6, 1), null, "Dr. Peeters", null)));
        var vaccineRecord = (await vaccineCreated.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childA.Id}/vaccine-records/{vaccineRecord.Id}/attachment-upload-url", org.AccessToken,
            new CreateVaccineRecordAttachmentUploadUrlRequest("image/jpeg")));

        return new Fixture(client, org.AccessToken, childA.Id, childB.Id, group.Id, soleActivity.Id, sharedActivity.Id, healthRecord.Id, vaccineRecord.Id);
    }

    [Fact]
    public async Task Purge_DeactivatedChild_DeletesSoleOwnedObjects_PreservesSharedPhoto()
    {
        var client = factory.CreateClient();
        var fixture = await BuildAsync(client);

        var deactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{fixture.ChildAId}/deactivate", fixture.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var purgeResponse = await PurgeAsync(client, fixture.AccessToken, fixture.ChildAId);
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
        var result = (await purgeResponse.Content.ReadFromJsonAsync<PurgePhotosResponse>())!;

        Assert.Empty(result.FailedObjectPaths);
        Assert.Equal(1, result.PreservedGroupPhotoCount);
        // Profile photo + health attachment + vaccine attachment + sole-photo full + thumbnail.
        Assert.Equal(5, result.DeletedObjectPaths.Count);

        var child = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{fixture.ChildAId}", fixture.AccessToken)))
            .Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(child.PhotoDownloadUrl);

        var healthRecords = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{fixture.ChildAId}/health-records", fixture.AccessToken)))
            .Content.ReadFromJsonAsync<List<HealthRecordResponse>>())!;
        Assert.Null(healthRecords.Single(r => r.Id == fixture.HealthRecordId).AttachmentDownloadUrl);

        var vaccineRecords = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{fixture.ChildAId}/vaccine-records", fixture.AccessToken)))
            .Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        Assert.Null(vaccineRecords.Single(r => r.Id == fixture.VaccineRecordId).AttachmentDownloadUrl);

        // The sole-depicted photo's activity now has zero photos; the shared activity's photo
        // is untouched (FR-016 — never delete an object still depicting another child).
        var soleTimeline = await GetDirectorTimelineAsync(client, fixture.AccessToken, fixture.GroupId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-50)));
        var soleTimelineBody = (await soleTimeline.Content.ReadFromJsonAsync<GroupTimelineResponse>())!;
        var soleEntry = soleTimelineBody.Entries.Single(e => e.GroupActivity?.Id == fixture.SoleActivityId);
        Assert.Empty(soleEntry.GroupActivity!.Photos);

        var sharedTimeline = await GetDirectorTimelineAsync(client, fixture.AccessToken, fixture.GroupId, DateOnly.FromDateTime(DateTime.UtcNow));
        var sharedTimelineBody = (await sharedTimeline.Content.ReadFromJsonAsync<GroupTimelineResponse>())!;
        var sharedEntry = sharedTimelineBody.Entries.Single(e => e.GroupActivity?.Id == fixture.SharedActivityId);
        Assert.Single(sharedEntry.GroupActivity!.Photos);
    }

    [Fact]
    public async Task Purge_ActiveChild_IsRejected_DeletesNothing()
    {
        var client = factory.CreateClient();
        var fixture = await BuildAsync(client);

        var response = await PurgeAsync(client, fixture.AccessToken, fixture.ChildAId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.children.still_active", await response.Content.ReadAsStringAsync());

        var child = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{fixture.ChildAId}", fixture.AccessToken)))
            .Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(child.PhotoDownloadUrl);
    }

    [Fact]
    public async Task Purge_PartialStorageFailure_SurfacedAsFailure_NeverReportedAsCleanSuccess()
    {
        var client = factory.CreateClient();
        var fixture = await BuildAsync(client);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{fixture.ChildAId}/deactivate", fixture.AccessToken));

        var fakeProfileStorage = factory.Services.GetRequiredService<FakeProfilePhotoStorage>();
        fakeProfileStorage.ThrowOnDelete = true;
        try
        {
            var purgeResponse = await PurgeAsync(client, fixture.AccessToken, fixture.ChildAId);
            Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
            var result = (await purgeResponse.Content.ReadFromJsonAsync<PurgePhotosResponse>())!;

            Assert.NotEmpty(result.FailedObjectPaths);
            Assert.DoesNotContain(result.FailedObjectPaths, p => result.DeletedObjectPaths.Contains(p));

            // Retrying after the storage failure clears is safe: the profile photo path is
            // treated as already-satisfied once actually deleted, everything else proceeds.
            fakeProfileStorage.ThrowOnDelete = false;
            var retryResponse = await PurgeAsync(client, fixture.AccessToken, fixture.ChildAId);
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
            var retryResult = (await retryResponse.Content.ReadFromJsonAsync<PurgePhotosResponse>())!;
            Assert.Empty(retryResult.FailedObjectPaths);
        }
        finally
        {
            fakeProfileStorage.ThrowOnDelete = false;
        }
    }
}
