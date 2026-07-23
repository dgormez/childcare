using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffScheduling;

/// <summary>
/// Feature 027/US3 (FR-005/FR-005a/FR-006/FR-007/FR-008a/FR-014/FR-018): sick report, eligible
/// cover candidates, and on-the-fly cover assignment.
/// </summary>
public class SickCoverAssignmentTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
        HttpClient client, string orgSlug, string directorAccessToken, Guid locationId, string firstName = "Care")
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest(firstName, "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        await AssignEligibilityAsync(client, directorAccessToken, staff.Id, locationId);

        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = System.Text.RegularExpressions.Regex.Match(entry.Message, @"token=([^&\s]+)");
        var token = match.Groups[1].Value;

        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    private async Task BackdateEntryAsync(Guid tenantId, Guid entryId, DateOnly date)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var entry = await db.StaffSchedules.FirstAsync(s => s.Id == entryId);
        entry.Date = date;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SetPushTokenAsync(Guid tenantId, Guid staffProfileId, string pushToken)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var profile = await db.StaffProfiles.FirstAsync(p => p.Id == staffProfileId);
        profile.PushToken = pushToken;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    // ReportSickCommand resolves "today" or "tomorrow" server-side from a fixed opening-time
    // cutoff (spec.md Assumptions) — rather than assume which one wins depending on wall-clock
    // time when the suite happens to run, seed an entry on BOTH candidate dates so the report
    // always has exactly one to flip, and return whichever one the server actually picked.
    private async Task<StaffScheduleResponse> SeedBothCandidateDatesAndReportSickAsync(
        HttpClient client, string directorToken, string staffToken, Guid tenantId, Guid staffId, Guid locationId, TimeOnly start, TimeOnly end)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var farFuture = today.AddDays(30);

        var todayEntryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", directorToken,
            new CreateStaffScheduleRequest(staffId, locationId, null, farFuture, start, end)));
        var todayEntry = (await todayEntryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        await BackdateEntryAsync(tenantId, todayEntry.Id, today);

        var tomorrowEntryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", directorToken,
            new CreateStaffScheduleRequest(staffId, locationId, null, farFuture.AddDays(1), start, end)));
        var tomorrowEntry = (await tomorrowEntryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        await BackdateEntryAsync(tenantId, tomorrowEntry.Id, tomorrow);

        var reportResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules/report-sick", staffToken));
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        return (await reportResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
    }

    [Fact]
    public async Task ReportSick_FlipsStatusToAbsent_CreatesApprovedLeaveRequest_AndIsIdempotent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sick Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var updated = await SeedBothCandidateDatesAndReportSickAsync(
            client, org.AccessToken, staffToken, org.Organisation.Id, staff.Id, location.Id, new TimeOnly(8, 0), new TimeOnly(16, 0));
        Assert.Equal("absent", updated.Status);

        var myLeaveRequests = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests/me", staffToken));
        var leaveRequests = (await myLeaveRequests.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Single(leaveRequests, r => r.Type == "sick" && r.Status == "approved");

        // FR-005a: a repeated call for the same already-absent day is idempotent — no duplicate
        // StaffLeaveRequest.
        var secondReport = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules/report-sick", staffToken));
        Assert.Equal(HttpStatusCode.OK, secondReport.StatusCode);

        var leaveRequestsAfterRepeat = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests/me", staffToken));
        var afterRepeat = (await leaveRequestsAfterRepeat.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Single(afterRepeat, r => r.Type == "sick");
    }

    [Fact]
    public async Task GetSickCoverCandidates_ExcludesIneligibleAndConflictingStaff()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sick Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");

        var (absentStaff, absentToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id, "Absent");
        var eligible = await CreateStaffAsync(client, org.AccessToken, "Eligible");
        await AssignEligibilityAsync(client, org.AccessToken, eligible.Id, location.Id);
        var ineligible = await CreateStaffAsync(client, org.AccessToken, "Ineligible");
        await AssignEligibilityAsync(client, org.AccessToken, ineligible.Id, otherLocation.Id);
        var conflicting = await CreateStaffAsync(client, org.AccessToken, "Conflicting");
        await AssignEligibilityAsync(client, org.AccessToken, conflicting.Id, location.Id);

        var absentEntry = await SeedBothCandidateDatesAndReportSickAsync(
            client, org.AccessToken, absentToken, org.Organisation.Id, absentStaff.Id, location.Id, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var conflictingEntryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(conflicting.Id, location.Id, null, absentEntry.Date.AddDays(30), new TimeOnly(9, 0), new TimeOnly(13, 0))));
        var conflictingEntry = (await conflictingEntryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        await BackdateEntryAsync(org.Organisation.Id, conflictingEntry.Id, absentEntry.Date);

        var candidatesResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/staff-schedules/{absentEntry.Date:yyyy-MM-dd}/sick-cover-candidates?excludeStaffProfileId={absentStaff.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);
        var candidates = (await candidatesResponse.Content.ReadFromJsonAsync<List<SickCoverCandidateResponse>>())!;

        Assert.Contains(candidates, c => c.StaffProfileId == eligible.Id);
        Assert.DoesNotContain(candidates, c => c.StaffProfileId == ineligible.Id);
        Assert.DoesNotContain(candidates, c => c.StaffProfileId == conflicting.Id);
        Assert.DoesNotContain(candidates, c => c.StaffProfileId == absentStaff.Id);
    }

    [Fact]
    public async Task AssignCover_SetsCoverStaffId_CreatesCoveredRow_NotifiesBoth_AndRejectsIneligible()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sick Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");

        var (absentStaff, absentToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id, "Absent");
        var cover = await CreateStaffAsync(client, org.AccessToken, "Cover");
        await AssignEligibilityAsync(client, org.AccessToken, cover.Id, location.Id);
        var ineligible = await CreateStaffAsync(client, org.AccessToken, "Ineligible");
        await AssignEligibilityAsync(client, org.AccessToken, ineligible.Id, otherLocation.Id);

        await SetPushTokenAsync(org.Organisation.Id, absentStaff.Id, "ExponentPushToken[absent]");
        await SetPushTokenAsync(org.Organisation.Id, cover.Id, "ExponentPushToken[cover]");

        var absentEntry = await SeedBothCandidateDatesAndReportSickAsync(
            client, org.AccessToken, absentToken, org.Organisation.Id, absentStaff.Id, location.Id, new TimeOnly(8, 0), new TimeOnly(16, 0));

        // FR-014's write-side enforcement: an ineligible coverStaffProfileId is rejected even
        // though it was never offered by GetSickCoverCandidatesQuery.
        var ineligibleAttempt = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{absentEntry.Id}/assign-cover", org.AccessToken,
            new AssignCoverRequest(ineligible.Id)));
        Assert.Equal(HttpStatusCode.Forbidden, ineligibleAttempt.StatusCode);

        var assignResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{absentEntry.Id}/assign-cover", org.AccessToken,
            new AssignCoverRequest(cover.Id)));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assignBody = (await assignResponse.Content.ReadFromJsonAsync<AssignCoverResponse>())!;

        Assert.Equal("absent", assignBody.Original.Status);
        Assert.Equal(cover.Id, assignBody.Original.CoverStaffId);
        Assert.Equal("covered", assignBody.CoverEntry.Status);
        Assert.Equal(cover.Id, assignBody.CoverEntry.StaffProfileId);
        Assert.True(assignBody.CoverEntry.IsPublished);

        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[absent]");
        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[cover]");
        // FR-008a: the absent staff member's push never names the replacement.
        Assert.DoesNotContain(pushSender.Sent, p => p.PushToken == "ExponentPushToken[absent]" && p.Body.Contains("Cover", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AssignCover_ConcurrentAttempts_OnlyOneSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sick Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "B");

        var (absentA, absentAToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, locationA.Id, "AbsentA");
        var (absentB, absentBToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, locationB.Id, "AbsentB");
        var cover = await CreateStaffAsync(client, org.AccessToken, "Cover");
        await AssignEligibilityAsync(client, org.AccessToken, cover.Id, locationA.Id);
        await AssignEligibilityAsync(client, org.AccessToken, cover.Id, locationB.Id);

        var entryA = await SeedBothCandidateDatesAndReportSickAsync(
            client, org.AccessToken, absentAToken, org.Organisation.Id, absentA.Id, locationA.Id, new TimeOnly(8, 0), new TimeOnly(16, 0));
        var entryB = await SeedBothCandidateDatesAndReportSickAsync(
            client, org.AccessToken, absentBToken, org.Organisation.Id, absentB.Id, locationB.Id, new TimeOnly(9, 0), new TimeOnly(13, 0));

        var task1 = client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{entryA.Id}/assign-cover", org.AccessToken, new AssignCoverRequest(cover.Id)));
        var task2 = client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{entryB.Id}/assign-cover", org.AccessToken, new AssignCoverRequest(cover.Id)));
        var results = await Task.WhenAll(task1, task2);

        // FR-018: both absences resolve to the same sick-report date (both calls run moments
        // apart against the same cutoff) with overlapping times (8-16 vs 9-13), so the same
        // replacement can only be assigned to one — exactly one call succeeds, the other is
        // rejected cleanly rather than double-booking the replacement.
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }
}
