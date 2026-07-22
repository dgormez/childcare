using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffLeaveRequests;

/// <summary>
/// Feature 027/US4 (FR-009/FR-010/FR-011/FR-011a): planned leave request submission and
/// director approval/rejection, including the "On Approved" rule's Covered-row skip.
/// </summary>
public class LeaveRequestApprovalTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

    private async Task<(ChildCare.Contracts.Responses.StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
        HttpClient client, string orgSlug, string directorAccessToken, Guid locationId)
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
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

    private static Task<HttpResponseMessage> CreateLeaveRequestAsync(HttpClient client, string accessToken, string type, DateOnly from, DateOnly to, string? notes = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-leave-requests", accessToken, new CreateLeaveRequestRequest(type, from, to, notes)));

    [Fact]
    public async Task Create_DateToBeforeDateFrom_ReturnsValidationError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Leave Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (_, token) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var response = await CreateLeaveRequestAsync(client, token, "annual", FutureDate.AddDays(3), FutureDate);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_RangeEntirelyInThePast_ReturnsValidationError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Leave Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (_, token) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        var response = await CreateLeaveRequestAsync(client, token, "annual", pastStart, pastEnd);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.staff_leave_requests.invalid_date_range", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Approval_MarksMatchingScheduleRowsAbsent_SkipsCoveredRow_LeavesUnscheduledDatesUntouched()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Leave Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var cover = await CreateStaffAsync(client, org.AccessToken, "Cover");
        await AssignEligibilityAsync(client, org.AccessToken, cover.Id, location.Id);

        // Monday-anchored so dateFrom..dateFrom+3 (Mon-Thu) never crosses a week boundary —
        // the list endpoint reads one Monday-anchored week at a time.
        var dateFrom = NextMonday();
        var dateTo = dateFrom.AddDays(3);

        // Day 0: a normal Scheduled entry -> should flip to Absent on approval.
        var scheduledEntryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(staff.Id, location.Id, null, dateFrom, new TimeOnly(8, 0), new TimeOnly(16, 0))));
        var scheduledEntry = (await scheduledEntryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;

        // Day 1: already Covered (an arranged replacement exists) -> FR-011a must skip it,
        // not overwrite CoverStaffId.
        var coveredEntryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(staff.Id, location.Id, null, dateFrom.AddDays(1), new TimeOnly(8, 0), new TimeOnly(16, 0))));
        var coveredEntry = (await coveredEntryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{coveredEntry.Id}/absence", org.AccessToken, new MarkAbsenceRequest(true, "sick")));
        var assignCoverResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{coveredEntry.Id}/assign-cover", org.AccessToken,
            new AssignCoverRequest(cover.Id)));
        Assert.Equal(HttpStatusCode.OK, assignCoverResponse.StatusCode);

        // Day 2 (dateFrom.AddDays(2)): no StaffSchedule row at all -> stays untouched, no row created.

        var createResponse = await CreateLeaveRequestAsync(client, staffToken, "annual", dateFrom, dateTo);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var leaveRequest = (await createResponse.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;

        var queueResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests?status=pending", org.AccessToken));
        var queue = (await queueResponse.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Contains(queue, r => r.Id == leaveRequest.Id);

        var decideResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-leave-requests/{leaveRequest.Id}/decide", org.AccessToken,
            new DecideLeaveRequestRequest(true)));
        Assert.Equal(HttpStatusCode.OK, decideResponse.StatusCode);
        var decided = (await decideResponse.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;
        Assert.Equal("approved", decided.Status);

        var listResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/staff-schedules?locationId={location.Id}&weekStart={MondayOf(dateFrom):yyyy-MM-dd}", org.AccessToken));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;

        var flippedEntry = entries.Single(e => e.Id == scheduledEntry.Id);
        Assert.Equal("absent", flippedEntry.Status);
        Assert.Equal("leave", flippedEntry.AbsenceReason);

        // FR-011a: the covered row is untouched — still Covered, CoverStaffId intact.
        var untouchedCoveredEntry = entries.Single(e => e.Id == coveredEntry.Id);
        Assert.Equal("absent", untouchedCoveredEntry.Status);
        Assert.Equal(cover.Id, untouchedCoveredEntry.CoverStaffId);

        // No row was created for the unscheduled day-2 date.
        Assert.DoesNotContain(entries, e => e.StaffProfileId == staff.Id && e.Date == dateFrom.AddDays(2));
    }

    [Fact]
    public async Task Rejection_MakesNoRotaChange()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Leave Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var entryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(staff.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0))));
        var entry = (await entryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;

        var createResponse = await CreateLeaveRequestAsync(client, staffToken, "other", FutureDate, FutureDate);
        var leaveRequest = (await createResponse.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;

        var decideResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-leave-requests/{leaveRequest.Id}/decide", org.AccessToken,
            new DecideLeaveRequestRequest(false)));
        Assert.Equal(HttpStatusCode.OK, decideResponse.StatusCode);
        var decided = (await decideResponse.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;
        Assert.Equal("rejected", decided.Status);

        var listResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/staff-schedules?locationId={location.Id}&weekStart={MondayOf(FutureDate):yyyy-MM-dd}", org.AccessToken));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        var untouched = entries.Single(e => e.Id == entry.Id);
        Assert.Equal("scheduled", untouched.Status);
    }

    [Fact]
    public async Task Decide_AlreadyDecidedRequest_Returns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Leave Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (_, staffToken) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var createResponse = await CreateLeaveRequestAsync(client, staffToken, "other", FutureDate, FutureDate);
        var leaveRequest = (await createResponse.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-leave-requests/{leaveRequest.Id}/decide", org.AccessToken, new DecideLeaveRequestRequest(true)));

        var secondDecision = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-leave-requests/{leaveRequest.Id}/decide", org.AccessToken, new DecideLeaveRequestRequest(false)));

        Assert.Equal(HttpStatusCode.Conflict, secondDecision.StatusCode);
        Assert.Contains("errors.staff_leave_requests.already_decided", await secondDecision.Content.ReadAsStringAsync());
    }

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static DateOnly NextMonday()
    {
        var today = FutureDate;
        var offset = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(offset == 0 ? 7 : offset);
    }
}
