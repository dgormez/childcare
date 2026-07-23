using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffScheduling;

/// <summary>
/// Feature 027/US1 (FR-001, FR-008, SC-004): a director publishes a week's rota, gating
/// GET /api/staff-schedules/me visibility and firing a SchedulePublished push per distinct
/// affected staff member.
/// </summary>
public class PublishVisibilityTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // Next Monday, always in the future regardless of when the suite runs.
    private static DateOnly NextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        var offset = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(offset == 0 ? 7 : offset);
    }

    private static Task<HttpResponseMessage> CreateEntryRawAsync(
        HttpClient client, string accessToken, Guid staffId, Guid locationId, DateOnly date, TimeOnly start, TimeOnly end) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", accessToken,
            new CreateStaffScheduleRequest(staffId, locationId, null, date, start, end)));

    private static async Task<StaffScheduleResponse> CreateEntryAsync(
        HttpClient client, string accessToken, Guid staffId, Guid locationId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var response = await CreateEntryRawAsync(client, accessToken, staffId, locationId, date, start, end);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
    }

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, string accessToken, Guid locationId, DateOnly weekStart, bool unpublish = false) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{locationId}/publish", accessToken,
            new PublishScheduleWeekRequest(weekStart, unpublish)));

    private async Task SetPushTokenAsync(Guid tenantId, Guid staffProfileId, string pushToken)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var profile = await db.StaffProfiles.FirstAsync(p => p.Id == staffProfileId);
        profile.PushToken = pushToken;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Publish_NonMondayWeekStart_Returns400()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Publish Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var notMonday = NextMonday().AddDays(1);

        var response = await PublishAsync(client, org.AccessToken, location.Id, notMonday);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Publish_PublishesEveryRowForLocationAndWeek()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Publish Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staffA = await CreateStaffAsync(client, org.AccessToken, "Anna");
        await AssignEligibilityAsync(client, org.AccessToken, staffA.Id, location.Id);
        var staffB = await CreateStaffAsync(client, org.AccessToken, "Bram");
        await AssignEligibilityAsync(client, org.AccessToken, staffB.Id, location.Id);
        var monday = NextMonday();

        await CreateEntryAsync(client, org.AccessToken, staffA.Id, location.Id, monday, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await CreateEntryAsync(client, org.AccessToken, staffB.Id, location.Id, monday.AddDays(1), new TimeOnly(8, 0), new TimeOnly(16, 0));

        var response = await PublishAsync(client, org.AccessToken, location.Id, monday);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PublishScheduleWeekResponse>())!;
        Assert.Equal(2, body.PublishedCount);
    }

    [Fact]
    public async Task UnpublishedWeek_ExcludedFromGetMySchedule_ThenVisibleAfterPublish_WithOnePushPerStaff()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Publish Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var monday = NextMonday();

        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);

        var entry = await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, monday, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, monday.AddDays(1), new TimeOnly(8, 0), new TimeOnly(16, 0));

        await SetPushTokenAsync(org.Organisation.Id, staff.Id, "ExponentPushToken[publish-test]");

        var inviteToken = await ExtractLatestStaffInviteTokenAsync(email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(org.Organisation.Slug, inviteToken, "password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var beforePublish = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", session.AccessToken));
        var beforeEntries = (await beforePublish.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.DoesNotContain(beforeEntries, e => e.Id == entry.Id);

        var publishResponse = await PublishAsync(client, org.AccessToken, location.Id, monday);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var afterPublish = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-schedules/me", session.AccessToken));
        var afterEntries = (await afterPublish.Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Contains(afterEntries, e => e.Id == entry.Id);
        Assert.Equal(2, afterEntries.Count);

        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Single(pushSender.Sent, p => p.PushToken == "ExponentPushToken[publish-test]");
    }

    private async Task<string> ExtractLatestStaffInviteTokenAsync(string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = System.Text.RegularExpressions.Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return await Task.FromResult(match.Groups[1].Value);
    }
}
