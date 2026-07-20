using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.Cli;

/// <summary>User Story 3 (031-photo-lifecycle-governance, spec.md FR-001 through FR-006): the
/// automatic `evaluate-photo-archival` job. Assertions key off each test's own object paths
/// (never dictionary/collection counts) since the shared IClassFixture factory means earlier
/// tests' tenants and objects get reprocessed on every RunAsync call too (matches
/// SendDailyReportsCommandTests' existing convention for the same reason).</summary>
public class EvaluatePhotoArchivalCommandTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task BackdateChildDeactivationAsync(string tenantSlug, Guid childId, int daysAgo)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Slug == tenantSlug);
        var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
        var child = await db.Children.SingleAsync(c => c.Id == childId);
        child.DeactivatedAt = DateTime.UtcNow.AddDays(-daysAgo);
        await db.SaveChangesAsync();
    }

    private async Task BackdatePhotoUploadAsync(string tenantSlug, Guid photoId, int daysAgo)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Slug == tenantSlug);
        var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
        var photo = await db.GroupActivityPhotos.SingleAsync(p => p.Id == photoId);
        photo.UploadedAt = DateTime.UtcNow.AddDays(-daysAgo);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ArchiveOnDeparture_ChildInactive31Days_TransitionsProfileAndAttachments_GroupPhotoWaitsForAllDepictedChildren()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Archival Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));

        var profileUrlResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/photo/upload-url", org.AccessToken));
        var profileUrl = (await profileUrlResponse.Content.ReadFromJsonAsync<RequestPhotoUploadUrlResponse>())!;

        var healthCreated = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        var healthRecord = (await healthCreated.Content.ReadFromJsonAsync<HealthRecordResponse>())!;
        var healthUploadUrl = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childA.Id}/health-records/{healthRecord.Id}/attachment-upload-url", org.AccessToken,
            new CreateHealthRecordAttachmentUploadUrlRequest("application/pdf")));
        var healthUploadBody = (await healthUploadUrl.Content.ReadFromJsonAsync<CreateHealthRecordAttachmentUploadUrlResponse>())!;

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "Walk");
        var uploaded = await UploadPhotoAsync(client, deviceToken, activity.Id);
        var photo = (await uploaded.Content.ReadFromJsonAsync<GroupActivityPhotoResponse>())!;

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/deactivate", org.AccessToken));
        await BackdateChildDeactivationAsync(org.Organisation.Slug, childA.Id, daysAgo: 31);

        var exitCode = await ChildCare.Api.Cli.EvaluatePhotoArchivalCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode);

        var profileStorage = factory.Services.GetRequiredService<FakeProfilePhotoStorage>();
        var healthStorage = factory.Services.GetRequiredService<FakeHealthAttachmentStorage>();
        var groupActivityStorage = factory.Services.GetRequiredService<FakeGroupActivityPhotoStorage>();

        Assert.Equal("COLDLINE", profileStorage.StorageClasses[profileUrl.ObjectPath]);

        var healthAttachmentPath = healthUploadBody.UploadUrl.Split('?')[0].Replace("https://fake-gcs.test/upload/", "");
        Assert.Equal("COLDLINE", healthStorage.StorageClasses[healthAttachmentPath]);

        // childB (still active) is also depicted in the same group photo — the whole photo
        // must wait until every depicted child is inactive (spec.md FR-002).
        Assert.False(groupActivityStorage.StorageClasses.ContainsKey(photo.Id.ToString()) ||
            groupActivityStorage.StorageClasses.Keys.Any(k => k.Contains(activity.Id.ToString()) && !k.Contains("-thumb")));

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childB.Id}/deactivate", org.AccessToken));
        await BackdateChildDeactivationAsync(org.Organisation.Slug, childB.Id, daysAgo: 31);

        var secondExitCode = await ChildCare.Api.Cli.EvaluatePhotoArchivalCommand.RunAsync(factory.Services);
        Assert.Equal(0, secondExitCode);

        var fullResolutionPath = $"group-activities/{activity.Id}/{photo.Id}.jpg";
        Assert.Equal("COLDLINE", groupActivityStorage.StorageClasses[fullResolutionPath]);
    }

    [Fact]
    public async Task ReactivatedChild_PhotoRemainsResolvable_NoExplicitUnArchiveStepNeeded()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Archival Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/photo/upload-url", org.AccessToken));

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        await BackdateChildDeactivationAsync(org.Organisation.Slug, child.Id, daysAgo: 31);
        await ChildCare.Api.Cli.EvaluatePhotoArchivalCommand.RunAsync(factory.Services);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/reactivate", org.AccessToken));

        var reactivated = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(reactivated.PhotoDownloadUrl);
    }

    [Fact]
    public async Task DeactivateChildCommand_Alone_NeverTransitionsOrDeletesAnything()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Archival Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var profileUrlResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/photo/upload-url", org.AccessToken));
        var profileUrl = (await profileUrlResponse.Content.ReadFromJsonAsync<RequestPhotoUploadUrlResponse>())!;

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));

        var profileStorage = factory.Services.GetRequiredService<FakeProfilePhotoStorage>();
        Assert.False(profileStorage.StorageClasses.ContainsKey(profileUrl.ObjectPath));

        var reread = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(reread.PhotoDownloadUrl);
    }

    [Fact]
    public async Task GeneralTiering_GroupActivityPhoto91DaysOld_StillActiveChild_TransitionsToNearline_RemainsResolvable()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Archival Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-120)));

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "Walk");
        var uploaded = await UploadPhotoAsync(client, deviceToken, activity.Id);
        var photo = (await uploaded.Content.ReadFromJsonAsync<GroupActivityPhotoResponse>())!;

        await BackdatePhotoUploadAsync(org.Organisation.Slug, photo.Id, daysAgo: 91);

        var exitCode = await ChildCare.Api.Cli.EvaluatePhotoArchivalCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode);

        var groupActivityStorage = factory.Services.GetRequiredService<FakeGroupActivityPhotoStorage>();
        var fullResolutionPath = $"group-activities/{activity.Id}/{photo.Id}.jpg";
        var thumbnailPath = $"group-activities/{activity.Id}/{photo.Id}-thumb.jpg";

        Assert.Equal("NEARLINE", groupActivityStorage.StorageClasses[fullResolutionPath]);
        Assert.False(groupActivityStorage.StorageClasses.ContainsKey(thumbnailPath)); // thumbnail never transitions (FR-004)

        var timeline = await GetDirectorTimelineAsync(client, org.AccessToken, group.Id, DateOnly.FromDateTime(DateTime.UtcNow));
        var timelineBody = (await timeline.Content.ReadFromJsonAsync<GroupTimelineResponse>())!;
        var entry = timelineBody.Entries.Single(e => e.GroupActivity?.Id == activity.Id);
        Assert.NotNull(Assert.Single(entry.GroupActivity!.Photos).DownloadUrl);
    }
}
