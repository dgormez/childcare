using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 1 (T014/T015c): planned_duration_minutes derivation from the child's
/// active contract, the null "extra day" case, and the split-location contract-matching rule
/// (FR-006).</summary>
public class PlannedDurationCalculatorTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // 2026-01-05 is a Monday.
    private static readonly DateOnly Monday = new(2026, 1, 5);

    [Fact]
    public async Task CheckIn_WithMatchingContractedDay_DerivesPlannedDurationFromContract()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Duration Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        // Days() defaults to 08:00–17:00 = 540 minutes.
        Assert.Equal(540, body.PlannedDurationMinutes);
    }

    [Fact]
    public async Task CheckIn_OnExtraDayWithNoContractedEntry_PlannedDurationIsNull()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Duration Extra Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        // Contract only covers Tuesday — Monday is an approved extra day.
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Tuesday);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        Assert.Null(body.PlannedDurationMinutes);
    }

    [Fact]
    public async Task CheckIn_AtDifferentLocationFromChildsContractedOne_DoesNotBorrowThatContractsDuration()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Duration SplitLoc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", locationB.Id);
        var child = await CreateChildAsync(client, org.AccessToken);

        // Child's real contract (Monday 08:00–17:00 = 540 min) is at Location A. Feature 007's
        // day-overlap rule means the child cannot also hold a Monday contract at Location B, so
        // a Monday visit to B is necessarily an uncontracted "extra day" there — the calculator
        // must not borrow Location A's Monday duration just because the weekday matches.
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, locationA.Id, DayOfWeek.Monday);

        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id);
        var response = await CheckInChildAsync(client, deviceTokenB, child.Id, Monday);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        Assert.Null(body.PlannedDurationMinutes);
    }
}
