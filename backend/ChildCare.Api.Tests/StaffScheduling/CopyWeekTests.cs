using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffScheduling;

/// <summary>User Story 2 (feature 012) — copying a week's rota forward, with closure-day
/// (FR-009) and existing-entry (FR-009a) conflicts skipped and reported, and target-week
/// validity enforced (FR-016).</summary>
public class CopyWeekTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static DateOnly NextMondayAtLeast(int minDaysAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(minDaysAhead);
        while (date.DayOfWeek != DayOfWeek.Monday)
            date = date.AddDays(1);
        return date;
    }

    private static DateOnly SourceWeekStart => NextMondayAtLeast(21);
    private static DateOnly TargetWeekStart => SourceWeekStart.AddDays(7);

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

    private static Task<HttpResponseMessage> CopyWeekRawAsync(HttpClient client, string accessToken, Guid locationId, DateOnly source, DateOnly target) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules/copy-week", accessToken, new CopyWeekRequest(locationId, source, target)));

    private static Task<HttpResponseMessage> ListAsync(HttpClient client, string accessToken, Guid locationId, DateOnly weekStart) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff-schedules?locationId={locationId}&weekStart={weekStart:yyyy-MM-dd}", accessToken));

    private static async Task PublishClosureDayAsync(HttpClient client, string accessToken, Guid locationId, DateOnly date)
    {
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/closures", accessToken,
            new CreateClosureDayRequest(locationId, date, "Team training", "training", false)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        var publishResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closure.Id}/publish", accessToken,
            new PublishClosureDayRequest(true)));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
    }

    [Fact]
    public async Task CopyWeek_FullWeek_ReplicatesEveryEntry()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Copy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staffA = await CreateStaffAsync(client, org.AccessToken, "Alice");
        await AssignEligibilityAsync(client, org.AccessToken, staffA.Id, location.Id);
        var staffB = await CreateStaffAsync(client, org.AccessToken, "Bob");
        await AssignEligibilityAsync(client, org.AccessToken, staffB.Id, location.Id);

        await CreateEntryAsync(client, org.AccessToken, staffA.Id, location.Id, SourceWeekStart, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await CreateEntryAsync(client, org.AccessToken, staffB.Id, location.Id, SourceWeekStart.AddDays(1), new TimeOnly(9, 0), new TimeOnly(17, 0));

        var copyResponse = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart, TargetWeekStart);
        Assert.Equal(HttpStatusCode.OK, copyResponse.StatusCode);
        var result = (await copyResponse.Content.ReadFromJsonAsync<CopyWeekResponse>())!;
        Assert.Equal(2, result.CopiedCount);
        Assert.Empty(result.Skipped);

        var targetEntries = (await (await ListAsync(client, org.AccessToken, location.Id, TargetWeekStart)).Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Contains(targetEntries, e => e.StaffProfileId == staffA.Id && e.Date == TargetWeekStart);
        Assert.Contains(targetEntries, e => e.StaffProfileId == staffB.Id && e.Date == TargetWeekStart.AddDays(1));
    }

    [Fact]
    public async Task CopyWeek_TargetHasClosureDay_SkipsAndReports()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Copy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);

        var sourceTuesday = SourceWeekStart.AddDays(1);
        var targetTuesday = TargetWeekStart.AddDays(1);
        await CreateEntryAsync(client, org.AccessToken, staff.Id, location.Id, sourceTuesday, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await PublishClosureDayAsync(client, org.AccessToken, location.Id, targetTuesday);

        var copyResponse = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart, TargetWeekStart);
        var result = (await copyResponse.Content.ReadFromJsonAsync<CopyWeekResponse>())!;

        Assert.Equal(0, result.CopiedCount);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("closure_day", skipped.Reason);
        Assert.Equal(targetTuesday, skipped.Date);
        Assert.Equal(staff.Id, skipped.StaffProfileId);
    }

    [Fact]
    public async Task CopyWeek_TargetHasExistingEntry_SkipsOnlyThatSlotAndCompletesTheRest()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Copy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");
        var staffA = await CreateStaffAsync(client, org.AccessToken, "Alice");
        await AssignEligibilityAsync(client, org.AccessToken, staffA.Id, location.Id);
        var staffB = await CreateStaffAsync(client, org.AccessToken, "Bob");
        await AssignEligibilityAsync(client, org.AccessToken, staffB.Id, location.Id);

        await CreateEntryAsync(client, org.AccessToken, staffA.Id, location.Id, SourceWeekStart, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await CreateEntryAsync(client, org.AccessToken, staffB.Id, location.Id, SourceWeekStart, new TimeOnly(8, 0), new TimeOnly(16, 0));
        // Pre-existing conflicting entry in the target week for staffA only.
        await CreateEntryAsync(client, org.AccessToken, staffA.Id, location.Id, TargetWeekStart, new TimeOnly(10, 0), new TimeOnly(14, 0));

        var copyResponse = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart, TargetWeekStart);
        var result = (await copyResponse.Content.ReadFromJsonAsync<CopyWeekResponse>())!;

        Assert.Equal(1, result.CopiedCount);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("existing_entry", skipped.Reason);
        Assert.Equal(staffA.Id, skipped.StaffProfileId);

        var targetEntries = (await (await ListAsync(client, org.AccessToken, location.Id, TargetWeekStart)).Content.ReadFromJsonAsync<List<StaffScheduleResponse>>())!;
        Assert.Contains(targetEntries, e => e.StaffProfileId == staffB.Id && e.Date == TargetWeekStart);
        // staffA's original pre-existing entry is untouched (never overwritten).
        Assert.Single(targetEntries, e => e.StaffProfileId == staffA.Id);
    }

    [Fact]
    public async Task CopyWeek_TargetNotAfterSource_Returns400InvalidCopyTarget()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Copy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");

        var sameWeek = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart, SourceWeekStart);
        var earlierWeek = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart, SourceWeekStart.AddDays(-7));

        Assert.Equal(HttpStatusCode.BadRequest, sameWeek.StatusCode);
        Assert.Contains("errors.staff_schedules.invalid_copy_target", await sameWeek.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, earlierWeek.StatusCode);
        Assert.Contains("errors.staff_schedules.invalid_copy_target", await earlierWeek.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CopyWeek_NonMondayWeekStart_Returns400Validation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Copy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "A");

        var nonMondaySource = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart.AddDays(1), TargetWeekStart);
        var nonMondayTarget = await CopyWeekRawAsync(client, org.AccessToken, location.Id, SourceWeekStart, TargetWeekStart.AddDays(1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, nonMondaySource.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nonMondayTarget.StatusCode);
    }
}
