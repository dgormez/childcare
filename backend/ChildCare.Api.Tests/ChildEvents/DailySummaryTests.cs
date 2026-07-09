using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 4 (T042-T044): daily summary aggregation and staff-internal exclusion (FR-017/FR-018).</summary>
public class DailySummaryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task DailySummary_AggregatesCountsAndLatestValues_Accurately()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = DateTime.UtcNow.Date.AddHours(10);

        await PostChildEventAsync(client, deviceToken, child.Id, "diaper", today, new { type = "wet" });
        await PostChildEventAsync(client, deviceToken, child.Id, "diaper", today.AddMinutes(30), new { type = "dirty" });
        await PostChildEventAsync(client, deviceToken, child.Id, "feeding_bottle", today.AddMinutes(45), new { ml = 120 });

        var sleepCreate = await PostChildEventAsync(client, deviceToken, child.Id, "sleep", today.AddHours(1), new { });
        var sleep = (await sleepCreate.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        await PatchChildEventAsDeviceAsync(client, deviceToken, sleep.Id, endedAt: today.AddHours(2), payload: new { quality = "good" });

        await PostChildEventAsync(client, deviceToken, child.Id, "mood", today.AddHours(3), new { value = "great" });
        await PostChildEventAsync(client, deviceToken, child.Id, "temperature", today.AddHours(3.5), new { celsius = 37.2 });
        await PostChildEventAsync(client, deviceToken, child.Id, "medication", today.AddHours(4), new { name = "perdolan", doseDescription = "5ml", reason = "fever" });

        var response = await GetDailySummaryAsync(client, deviceToken, child.Id, DateOnly.FromDateTime(today));
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;

        Assert.Equal(1, summary.NapsCount);
        Assert.Equal(1, summary.BottlesCount);
        Assert.Equal(2, summary.DiaperChangesCount);
        Assert.Equal("great", summary.LatestMood);
        Assert.Equal(37.2m, summary.LatestTemperatureCelsius);
        Assert.True(summary.MedicationAdministered);
    }

    [Fact]
    public async Task DailySummary_ExcludesStaffInternalEvent_FromCountsAndLatestValues()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SummaryExclude Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = DateTime.UtcNow.Date.AddHours(10);

        await PostChildEventAsync(client, deviceToken, child.Id, "mood", today, new { value = "great" }, visibleToParent: true);
        // A later, staff-internal mood must never surface as "latest".
        await PostChildEventAsync(client, deviceToken, child.Id, "mood", today.AddHours(1), new { value = "difficult" }, visibleToParent: false);

        var response = await GetDailySummaryAsync(client, deviceToken, child.Id, DateOnly.FromDateTime(today));
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;
        Assert.Equal("great", summary.LatestMood);
    }

    [Fact]
    public async Task DailySummary_NoEventsForChildOrDate_ReturnsZeroedSummary_Never404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SummaryEmpty Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await GetDailySummaryAsync(client, deviceToken, child.Id, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;
        Assert.Equal(0, summary.NapsCount);
        Assert.Null(summary.LatestMood);
        Assert.False(summary.MedicationAdministered);
    }
}
