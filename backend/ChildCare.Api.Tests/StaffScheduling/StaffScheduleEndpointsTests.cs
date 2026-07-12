using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffScheduling;

/// <summary>
/// Feature 012 — director-only weekly staff rota: create/edit/delete (US1), overlap and
/// past-date rules (US1), absence marking and the projected on-duty count (US3), read-endpoint
/// authorization (FR-015), and the caregiver's own-schedule read (US4). Rota-copy has its own
/// file (CopyWeekTests.cs) and the feature-010 decoupling regression has its own file
/// (BkrDecouplingTests.cs).
/// </summary>
public class StaffScheduleEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

    private static Task<HttpResponseMessage> CreateEntryRawAsync(
        HttpClient client, string accessToken, Guid staffId, Guid locationId, Guid? groupId, DateOnly date, TimeOnly start, TimeOnly end) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", accessToken,
            new CreateStaffScheduleRequest(staffId, locationId, groupId, date, start, end)));

    private static async Task<StaffScheduleResponse> CreateEntryAsync(
        HttpClient client, string accessToken, Guid staffId, Guid locationId, Guid? groupId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var response = await CreateEntryRawAsync(client, accessToken, staffId, locationId, groupId, date, start, end);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
    }

    /// <summary>Creates a staff member and assigns StaffLocationEligibility at every given
    /// location — most tests need this now that create/update enforce eligibility
    /// (convergence finding F1), mirroring VerifyPinCommand's existing check-in rule.</summary>
    private static async Task<StaffResponse> CreateEligibleStaffAsync(
        HttpClient client, string accessToken, IEnumerable<Guid> locationIds, string firstName = "Jane")
    {
        var staff = await CreateStaffAsync(client, accessToken, firstName);
        foreach (var locationId in locationIds)
            await AssignEligibilityAsync(client, accessToken, staff.Id, locationId);
        return staff;
    }

    private static Task<StaffResponse> CreateEligibleStaffAsync(HttpClient client, string accessToken, Guid locationId, string firstName = "Jane") =>
        CreateEligibleStaffAsync(client, accessToken, [locationId], firstName);

    private static Task<HttpResponseMessage> ListAsync(HttpClient client, string accessToken, Guid locationId, DateOnly weekStart) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff-schedules?locationId={locationId}&weekStart={weekStart:yyyy-MM-dd}", accessToken));

    private static Task<HttpResponseMessage> ProjectedOnDutyAsync(HttpClient client, string accessToken, Guid locationId, DateOnly date, TimeOnly time) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff-schedules/projected-on-duty?locationId={locationId}&date={date:yyyy-MM-dd}&time={time:HH:mm}", accessToken));

    private static Task<HttpResponseMessage> MarkAbsenceAsync(HttpClient client, string accessToken, Guid entryId, bool isAbsent, string? reason) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{entryId}/absence", accessToken, new MarkAbsenceRequest(isAbsent, reason)));

    private async Task<string> ExtractLatestStaffInviteTokenAsync(string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return await Task.FromResult(match.Groups[1].Value);
    }

    /// <summary>Creates a caregiver, assigns eligibility at the given location(s), accepts
    /// their invitation, and returns their own access token — mirrors
    /// StaffMeTests/CaregiverReadScopingTests' precedent.</summary>
    private async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
        HttpClient client, string orgSlug, string directorAccessToken, params Guid[] locationIds)
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        foreach (var locationId in locationIds)
            await AssignEligibilityAsync(client, directorAccessToken, staff.Id, locationId);

        var token = await ExtractLatestStaffInviteTokenAsync(email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    private async Task BackdateEntryAsync(Guid tenantId, Guid entryId, DateOnly pastDate)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var entry = await db.StaffSchedules.FirstAsync(s => s.Id == entryId);
        entry.Date = pastDate;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    // ── US1: create/list, multi-location, overlap, past-date, mid-week addition ─────────────

    [Fact]
    public async Task Create_ThenList_ReturnsEntry()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);

        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var listResponse = await ListAsync(client, org.AccessToken, location.Id, FutureDate.AddDays(-(((int)FutureDate.DayOfWeek + 6) % 7)));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Contains(entries, e => e.Id == entry.Id);
    }

    [Fact]
    public async Task Create_MultiLocationNonOverlapping_BothSaved()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "B");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, [locationA.Id, locationB.Id]);

        var first = await CreateEntryRawAsync(client, org.AccessToken, staff.Id, locationA.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var second = await CreateEntryRawAsync(client, org.AccessToken, staff.Id, locationB.Id, null, FutureDate, new TimeOnly(13, 0), new TimeOnly(17, 0));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Create_OverlappingCrossLocation_Returns409Overlap()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "B");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, [locationA.Id, locationB.Id]);

        await CreateEntryAsync(client, org.AccessToken, staff.Id, locationA.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var overlapping = await CreateEntryRawAsync(client, org.AccessToken, staff.Id, locationB.Id, null, FutureDate, new TimeOnly(11, 0), new TimeOnly(15, 0));

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Contains("errors.staff_schedules.overlap", await overlapping.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_OverlappingSameLocation_Returns409Overlap()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);

        await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, groupA.Id, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var overlapping = await CreateEntryRawAsync(client, org.AccessToken, staff.Id, location.Id, groupB.Id, FutureDate, new TimeOnly(11, 0), new TimeOnly(15, 0));

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Contains("errors.staff_schedules.overlap", await overlapping.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_ConcurrentOverlappingWrites_OnlyOneSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "B");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, [locationA.Id, locationB.Id]);

        var task1 = CreateEntryRawAsync(client, org.AccessToken, staff.Id, locationA.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var task2 = CreateEntryRawAsync(client, org.AccessToken, staff.Id, locationB.Id, null, FutureDate, new TimeOnly(9, 0), new TimeOnly(13, 0));
        var results = await Task.WhenAll(task1, task2);

        // FR-003/research.md R2: IAdvisoryLockService serializes the two overlap checks — exactly
        // one create wins, the other is rejected, never both silently succeeding.
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_FutureDatedEntry_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/staff-schedules/{entry.Id}", org.AccessToken,
            new UpdateStaffScheduleRequest(location.Id, null, new TimeOnly(9, 0), new TimeOnly(13, 0))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        Assert.Equal(new TimeOnly(9, 0), updated.StartTime);
    }

    [Fact]
    public async Task Update_PastDatedEntry_Returns400PastDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        await BackdateEntryAsync(org.Organisation.Id, entry.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/staff-schedules/{entry.Id}", org.AccessToken,
            new UpdateStaffScheduleRequest(location.Id, null, new TimeOnly(9, 0), new TimeOnly(13, 0))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.staff_schedules.past_date", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_PastDatedEntry_Returns400PastDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        await BackdateEntryAsync(org.Organisation.Id, entry.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff-schedules/{entry.Id}", org.AccessToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.staff_schedules.past_date", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_FutureDatedEntry_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff-schedules/{entry.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── FR-014/FR-015: DirectorOnly on all writes and reads (except FR-012's /me) ───────────

    [Fact]
    public async Task Writes_AsCaregiverToken_Return403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var create = await CreateEntryRawAsync(client, caregiverToken, staff.Id, location.Id, null, FutureDate.AddDays(1), new TimeOnly(8, 0), new TimeOnly(12, 0));
        var update = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/staff-schedules/{entry.Id}", caregiverToken,
            new UpdateStaffScheduleRequest(location.Id, null, new TimeOnly(9, 0), new TimeOnly(13, 0))));
        var delete = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff-schedules/{entry.Id}", caregiverToken));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Reads_AsCaregiverToken_Return403()
    {
        // FR-015: only GET /api/staff-schedules/me is StaffOrDirector — list and
        // projected-on-duty remain DirectorOnly like every write.
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken);

        var list = await ListAsync(client, caregiverToken, location.Id, FutureDate);
        var projected = await ProjectedOnDutyAsync(client, caregiverToken, location.Id, FutureDate, new TimeOnly(10, 0));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, projected.StatusCode);
    }

    // ── FR-015 (convergence F1): create/update against a location the staff member isn't ────
    // eligible for is rejected, consistent with VerifyPinCommand's existing NotEligible rule.

    [Fact]
    public async Task Create_ForIneligibleLocation_Returns403NotEligible()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var eligibleLocation = await CreateLocationAsync(client, org.AccessToken, "Eligible");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, eligibleLocation.Id);

        var response = await CreateEntryRawAsync(client, org.AccessToken, staff.Id, otherLocation.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.staff_schedules.not_eligible", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_IntoIneligibleLocation_Returns403NotEligible()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var eligibleLocation = await CreateLocationAsync(client, org.AccessToken, "Eligible");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, eligibleLocation.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, eligibleLocation.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(12, 0));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/staff-schedules/{entry.Id}", org.AccessToken,
            new UpdateStaffScheduleRequest(otherLocation.Id, null, new TimeOnly(9, 0), new TimeOnly(13, 0))));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.staff_schedules.not_eligible", await response.Content.ReadAsStringAsync());
    }

    // ── US3: absence marking and the projected on-duty count ────────────────────────────────

    [Fact]
    public async Task MarkAbsent_ExcludesFromProjectedOnDuty_AndUnmarkReverts()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));
        var noon = new TimeOnly(12, 0);

        var beforeAbsence = await (await ProjectedOnDutyAsync(client, org.AccessToken, location.Id, FutureDate, noon)).Content.ReadFromJsonAsync<ProjectedOnDutyResponse>();
        Assert.Contains(staff.Id, beforeAbsence!.StaffProfileIds);

        var markResponse = await MarkAbsenceAsync(client, org.AccessToken, entry.Id, true, "sick");
        Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);
        var afterAbsence = await (await ProjectedOnDutyAsync(client, org.AccessToken, location.Id, FutureDate, noon)).Content.ReadFromJsonAsync<ProjectedOnDutyResponse>();
        Assert.DoesNotContain(staff.Id, afterAbsence!.StaffProfileIds);

        var unmarkResponse = await MarkAbsenceAsync(client, org.AccessToken, entry.Id, false, null);
        Assert.Equal(HttpStatusCode.OK, unmarkResponse.StatusCode);
        var afterUnmark = await (await ProjectedOnDutyAsync(client, org.AccessToken, location.Id, FutureDate, noon)).Content.ReadFromJsonAsync<ProjectedOnDutyResponse>();
        Assert.Contains(staff.Id, afterUnmark!.StaffProfileIds);
    }

    [Fact]
    public async Task MarkAbsent_WithoutReason_Returns400Validation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var response = await MarkAbsenceAsync(client, org.AccessToken, entry.Id, true, null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MarkAbsent_OnPastDatedEntry_Returns400PastDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await BackdateEntryAsync(org.Organisation.Id, entry.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        var response = await MarkAbsenceAsync(client, org.AccessToken, entry.Id, true, "sick");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.staff_schedules.past_date", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProjectedOnDuty_ExcludesStudentVolunteer()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var volunteerResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Vol", "Unteer", $"vol_{Guid.NewGuid():N}@test.com", "+32 9 123 45 67", "StudentVolunteer", "Staff", null)));
        var volunteer = (await volunteerResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        await AssignEligibilityAsync(client, org.AccessToken, volunteer.Id, location.Id);
        await CreateEntryAsync(client, org.AccessToken, volunteer.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var response = await ProjectedOnDutyAsync(client, org.AccessToken, location.Id, FutureDate, new TimeOnly(10, 0));
        var projected = (await response.Content.ReadFromJsonAsync<ProjectedOnDutyResponse>())!;

        Assert.DoesNotContain(volunteer.Id, projected.StaffProfileIds);
    }

    [Fact]
    public async Task ProjectedOnDuty_ExcludesDeactivatedStaff_WithoutDeletingEntry()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var projected = await (await ProjectedOnDutyAsync(client, org.AccessToken, location.Id, FutureDate, new TimeOnly(10, 0))).Content.ReadFromJsonAsync<ProjectedOnDutyResponse>();
        Assert.DoesNotContain(staff.Id, projected!.StaffProfileIds);

        // FR-009b: the entry itself is still visible in the rota, not deleted.
        var listResponse = await ListAsync(client, org.AccessToken, location.Id, FutureDate.AddDays(-(((int)FutureDate.DayOfWeek + 6) % 7)));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Contains(entries, e => e.StaffProfileId == staff.Id);
    }

    // ── US4: caregiver's own schedule (FR-012) ───────────────────────────────────────────────

    [Fact]
    public async Task GetMySchedule_ReturnsOnlyOwnEntries()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var (caregiverA, caregiverAToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var (caregiverB, _) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var ownEntry = await CreateEntryAsync(client, org.AccessToken, caregiverA.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await CreateEntryAsync(client, org.AccessToken, caregiverB.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", caregiverAToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = (await response.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;

        Assert.Contains(entries, e => e.Id == ownEntry.Id);
        Assert.All(entries, e => Assert.Equal(caregiverA.Id, e.StaffProfileId));
    }

    [Fact]
    public async Task GetMySchedule_NoEntries_ReturnsEmptyArray()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", caregiverToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = (await response.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetMySchedule_AsDirectorWithNoStaffProfile_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", org.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.staff.profile_not_found", await response.Content.ReadAsStringAsync());
    }

    // ── Polish: FR-011 mid-week addition, unassigned group ───────────────────────────────────

    [Fact]
    public async Task Create_ForNewlyAddedStaffMidWeek_IsImmediatelySchedulable()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");

        // "Mid-week addition" — a staff member created after the current week has already
        // started must be immediately schedulable, with no separate activation step (FR-011).
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);
        var response = await CreateEntryRawAsync(client, org.AccessToken, staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnassignedGroup_DoesNotBlockProjectedOnDuty()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rota Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateEligibleStaffAsync(client, org.AccessToken, location.Id);

        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, groupId: null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0));
        Assert.Null(entry.GroupId);

        var projected = await (await ProjectedOnDutyAsync(client, org.AccessToken, location.Id, FutureDate, new TimeOnly(10, 0))).Content.ReadFromJsonAsync<ProjectedOnDutyResponse>();
        Assert.Contains(staff.Id, projected!.StaffProfileIds);
    }
}
