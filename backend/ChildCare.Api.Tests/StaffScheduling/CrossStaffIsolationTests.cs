using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffScheduling;

/// <summary>
/// Feature 027/US2 (FR-015/FR-015a): a schedule/leave-request read never returns another staff
/// member's rows, and staff-initiated writes (sick report, leave request) always act on the
/// JWT-resolved staff profile, never a client-supplied identifier — there is no such parameter
/// on any staff-facing route to begin with, so this proves the *effect* (each write only ever
/// touches the caller's own data) rather than any spoofable field.
/// </summary>
public class CrossStaffIsolationTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

    private async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
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

    [Fact]
    public async Task GetMySchedule_NeverReturnsAnotherStaffMembersRows()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Isolation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staffA, tokenA) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var (staffB, tokenB) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var entryA = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(staffA.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0))));
        var responseA = (await entryA.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(staffB.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0))));

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{location.Id}/publish", org.AccessToken,
            new PublishScheduleWeekRequest(MondayOf(FutureDate), false)));

        var meA = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", tokenA));
        var entriesA = (await meA.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;

        Assert.All(entriesA, e => Assert.Equal(staffA.Id, e.StaffProfileId));
        Assert.Contains(entriesA, e => e.Id == responseA.Id);

        var meB = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", tokenB));
        var entriesB = (await meB.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.All(entriesB, e => Assert.Equal(staffB.Id, e.StaffProfileId));
        Assert.DoesNotContain(entriesB, e => e.Id == responseA.Id);
    }

    [Fact]
    public async Task GetMyLeaveRequests_NeverReturnsAnotherStaffMembersRows()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Isolation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (_, tokenA) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var (_, tokenB) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var authedCreate = new HttpRequestMessage(HttpMethod.Post, "/api/staff-leave-requests")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new CreateLeaveRequestRequest("annual", FutureDate, FutureDate.AddDays(2), null)),
        };
        authedCreate.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);
        var createdA = await client.SendAsync(authedCreate);
        Assert.Equal(HttpStatusCode.Created, createdA.StatusCode);
        var requestA = (await createdA.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;

        var meA = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests/me", tokenA));
        var entriesA = (await meA.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Contains(entriesA, r => r.Id == requestA.Id);

        var meB = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests/me", tokenB));
        var entriesB = (await meB.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.DoesNotContain(entriesB, r => r.Id == requestA.Id);
        Assert.Empty(entriesB);
    }

    [Fact]
    public async Task ReportSick_OnlyAffectsCallersOwnAssignment()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Isolation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staffA, tokenA) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var (staffB, _) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var entryB = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(staffB.Id, location.Id, null, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0))));
        Assert.Equal(HttpStatusCode.Created, entryB.StatusCode);

        // staffA has no assignment "today" at all — report-sick must still succeed (204, per
        // FR-005a) and must never touch staffB's unrelated future entry.
        var sickResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules/report-sick", tokenA));
        Assert.True(sickResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var listResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/staff-schedules?locationId={location.Id}&weekStart={MondayOf(FutureDate):yyyy-MM-dd}", org.AccessToken));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Contains(entries, e => e.StaffProfileId == staffB.Id && e.Status == "scheduled");
    }

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
