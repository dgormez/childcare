using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 4 (031-photo-lifecycle-governance, spec.md FR-011): staff (not just directors) can
/// create/edit/delete health records and vaccine records, and delete group-activity photos,
/// within their assigned location(s) — the same location-scoping GetChildByIdQuery already
/// enforces elsewhere, applied consistently across all three routes this feature widens from
/// DirectorOnly to StaffOrDirector.
/// </summary>
public class PhotoRbacParityTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static string ExtractLatestStaffInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    /// <summary>Creates a staff profile, assigns it to the given location(s), accepts the
    /// invitation, and logs in — returning the staff's own access token.</summary>
    private static async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginStaffAsync(
        HttpClient client, OrganisationOnboardingWebAppFactory factory, string orgSlug, string directorAccessToken, params Guid[] locationIds)
    {
        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        foreach (var locationId in locationIds)
            await AssignEligibilityAsync(client, directorAccessToken, staff.Id, locationId);

        var token = ExtractLatestStaffInviteToken(factory, email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    private static async Task<Guid> AssignChildToLocationAsync(HttpClient client, string directorToken, Guid locationId, string groupName = "Room")
    {
        var group = await CreateGroupAsync(client, directorToken, groupName, locationId);
        var child = await CreateChildAsync(client, directorToken);
        await AssignChildToGroupAsync(client, directorToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        return child.Id;
    }

    // ── Staff can now create/edit/delete health & vaccine records, delete group-activity photos ──

    [Fact]
    public async Task Staff_CanCreateEditDeleteHealthRecord_AtAssignedLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var childId = await AssignChildToLocationAsync(client, org.AccessToken, location.Id);
        var (_, staffToken) = await CreateAndLoginStaffAsync(client, factory, org.Organisation.Slug, org.AccessToken, location.Id);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/health-records", staffToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var uploadUrl = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/health-records/{record.Id}/attachment-upload-url", staffToken,
            new CreateHealthRecordAttachmentUploadUrlRequest("application/pdf")));
        Assert.Equal(HttpStatusCode.OK, uploadUrl.StatusCode);

        var updated = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{childId}/health-records/{record.Id}", staffToken,
            new UpdateHealthRecordRequest("doctor_note", "Updated title", "Updated description.", null, null)));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deleted = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{childId}/health-records/{record.Id}", staffToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Staff_CanCreateEditDeleteVaccineRecord_AtAssignedLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var childId = await AssignChildToLocationAsync(client, org.AccessToken, location.Id);
        var (_, staffToken) = await CreateAndLoginStaffAsync(client, factory, org.Organisation.Slug, org.AccessToken, location.Id);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/vaccine-records", staffToken,
            new CreateVaccineRecordRequest("DTP", 2, new DateOnly(2026, 6, 1), null, "Dr. Peeters", null)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;

        var uploadUrl = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/vaccine-records/{record.Id}/attachment-upload-url", staffToken,
            new CreateVaccineRecordAttachmentUploadUrlRequest("image/jpeg")));
        Assert.Equal(HttpStatusCode.OK, uploadUrl.StatusCode);

        var updated = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{childId}/vaccine-records/{record.Id}", staffToken,
            new UpdateVaccineRecordRequest("DTP", 3, new DateOnly(2026, 6, 1), null, "Dr. Peeters", null)));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deleted = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{childId}/vaccine-records/{record.Id}", staffToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Staff_CanDeleteGroupActivity_AtAssignedLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "Walk");

        var (_, staffToken) = await CreateAndLoginStaffAsync(client, factory, org.Organisation.Slug, org.AccessToken, location.Id);

        var deleted = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/group-activities/{activity.Id}", staffToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    // ── Parent is denied all write actions on all three photo/record types (regression) ──────────

    [Fact]
    public async Task Parent_DeniedAllWriteActions_OnAllThreePhotoTypes()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "Walk");

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var createHealth = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", parentToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, createHealth.StatusCode);

        var createVaccine = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", parentToken,
            new CreateVaccineRecordRequest("DTP", 2, new DateOnly(2026, 6, 1), null, "Dr. Peeters", null)));
        Assert.Equal(HttpStatusCode.Forbidden, createVaccine.StatusCode);

        var deleteActivity = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/group-activities/{activity.Id}", parentToken));
        Assert.Equal(HttpStatusCode.Forbidden, deleteActivity.StatusCode);
    }

    // ── Staff denied at a location they are not assigned to (regression on existing scoping) ────

    [Fact]
    public async Task Staff_DeniedAllActions_ForUnassignedLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var childId = await AssignChildToLocationAsync(client, org.AccessToken, locationB.Id, "Room B");
        var groupB = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/groups", org.AccessToken)))
            .Content.ReadFromJsonAsync<List<GroupResponse>>())!.Single(g => g.LocationId == locationB.Id);
        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id);
        var activityB = await CreateGroupActivityOkAsync(client, deviceTokenB, "outdoor", "Walk");

        // Staff only assigned to Location A, not Location B.
        var (_, staffToken) = await CreateAndLoginStaffAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id);

        var createHealth = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/health-records", staffToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        Assert.Equal(HttpStatusCode.NotFound, createHealth.StatusCode);

        var createVaccine = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/vaccine-records", staffToken,
            new CreateVaccineRecordRequest("DTP", 2, new DateOnly(2026, 6, 1), null, "Dr. Peeters", null)));
        Assert.Equal(HttpStatusCode.NotFound, createVaccine.StatusCode);

        var deleteActivity = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/group-activities/{activityB.Id}", staffToken));
        Assert.Equal(HttpStatusCode.NotFound, deleteActivity.StatusCode);
    }

    // ── Staff assigned to multiple locations is allowed at every one of them ─────────────────────

    [Fact]
    public async Task Staff_AssignedToMultipleLocations_AllowedAtEveryAssignedLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var childAId = await AssignChildToLocationAsync(client, org.AccessToken, locationA.Id, "Room A");
        var childBId = await AssignChildToLocationAsync(client, org.AccessToken, locationB.Id, "Room B");

        var (_, staffToken) = await CreateAndLoginStaffAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id, locationB.Id);

        var createHealthA = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childAId}/health-records", staffToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        Assert.Equal(HttpStatusCode.Created, createHealthA.StatusCode);

        var createHealthB = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childBId}/health-records", staffToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        Assert.Equal(HttpStatusCode.Created, createHealthB.StatusCode);
    }

    // ── Authorization is evaluated against the actor's CURRENT location assignment at the time
    // of the action, not their assignment at the record's original creation time (Edge Cases) ───

    [Fact]
    public async Task Delete_EvaluatedAgainstCurrentLocationAssignment_NotCreationTimeAssignment()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rbac Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var childId = await AssignChildToLocationAsync(client, org.AccessToken, location.Id);

        // Director creates the record (directors are never location-scoped).
        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        // Staff not yet assigned to this location — denied.
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var deniedDelete = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{childId}/health-records/{record.Id}", staffToken));
        Assert.Equal(HttpStatusCode.NotFound, deniedDelete.StatusCode);

        // Assigning the staff to the location now — evaluated at delete time, not creation time
        // — makes the same pre-existing record deletable.
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);
        var allowedDelete = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{childId}/health-records/{record.Id}", staffToken));
        Assert.Equal(HttpStatusCode.NoContent, allowedDelete.StatusCode);
    }
}
