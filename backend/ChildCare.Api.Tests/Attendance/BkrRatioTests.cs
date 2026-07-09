using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 2 (T023-T026b): live BKR ratio threshold boundaries, zero-staff breach,
/// StudentVolunteer exclusion, and nap-time inference.</summary>
public class BkrRatioTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Today = ChildCare.Application.Common.BelgianCalendarDay.Today();

    private async Task<(HttpClient Client, ChildCare.Contracts.Responses.RegisterOrganisationResponse Org, LocationResponse Location, GroupResponse Group, string DeviceToken)>
        SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Bkr Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        return (client, org, location, group, deviceToken);
    }

    private async Task CheckInNChildrenAsync(HttpClient client, string deviceToken, string accessToken, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var child = await CreateChildAsync(client, accessToken, $"Child{i}_{Guid.NewGuid():N}");
            await CheckInChildAsync(client, deviceToken, child.Id, Today);
        }
    }

    private static async Task<BkrRatioResponse> GetBkrResponseAsync(HttpClient client, string deviceToken, Guid locationId)
    {
        var response = await AttendanceTestSupport.GetBkrAsync(client, deviceToken, locationId);
        return (await response.Content.ReadFromJsonAsync<BkrRatioResponse>())!;
    }

    /// <summary>All N children present with an open sleep event — guarantees nap time is
    /// inferred (100% ≥ 50%), used by the nap-threshold boundary tests below.</summary>
    private async Task CheckInNNappingChildrenAsync(HttpClient client, string deviceToken, string accessToken, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var child = await CreateChildAsync(client, accessToken, $"NapBoundaryChild{i}_{Guid.NewGuid():N}");
            await CheckInChildAsync(client, deviceToken, child.Id, Today);
            await PostChildEventAsync(client, deviceToken, child.Id, "sleep", DateTime.UtcNow, new { quality = (string?)null });
        }
    }

    [Theory]
    [InlineData(7, "green")]
    [InlineData(8, "amber")]
    [InlineData(9, "red")]
    public async Task SoloQualifiedCaregiver_NonNap_ReflectsThresholdBoundary(int presentCount, string expectedStatus)
    {
        var (client, org, location, group, deviceToken) = await SetupAsync();
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        await CheckInAsync(client, deviceToken, caregiver.Id, "1234");

        await CheckInNChildrenAsync(client, deviceToken, org.AccessToken, presentCount);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.Equal(presentCount, bkr.PresentCount);
        Assert.Equal(1, bkr.QualifiedStaffCount);
        Assert.Equal(8, bkr.Threshold);
        Assert.Equal(expectedStatus, bkr.Status);
    }

    [Theory]
    [InlineData(17, "green")]
    [InlineData(18, "amber")]
    [InlineData(19, "red")]
    public async Task TwoQualifiedCaregivers_NonNap_ReflectsThresholdBoundary(int presentCount, string expectedStatus)
    {
        var (client, org, location, group, deviceToken) = await SetupAsync();
        var caregiver1 = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111", "Anna");
        var caregiver2 = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2222", "Bram");
        await CheckInAsync(client, deviceToken, caregiver1.Id, "1111");
        await CheckInAsync(client, deviceToken, caregiver2.Id, "2222");

        await CheckInNChildrenAsync(client, deviceToken, org.AccessToken, presentCount);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.Equal(2, bkr.QualifiedStaffCount);
        Assert.Equal(18, bkr.Threshold);
        Assert.Equal(expectedStatus, bkr.Status);
    }

    // SC-003: the nap-time threshold's own green/amber/red boundary (13/14/15), distinct from
    // the non-nap boundary above — same threshold-comparison code path, different threshold
    // value (14 vs 8), so both are worth asserting explicitly per SC-003's enumerated list.
    [Theory]
    [InlineData(13, "green")]
    [InlineData(14, "amber")]
    [InlineData(15, "red")]
    public async Task SoloQualifiedCaregiver_NapTime_ReflectsThresholdBoundary(int presentCount, string expectedStatus)
    {
        var (client, org, location, group, deviceToken) = await SetupAsync();
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        await CheckInAsync(client, deviceToken, caregiver.Id, "1234");

        await CheckInNNappingChildrenAsync(client, deviceToken, org.AccessToken, presentCount);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.True(bkr.IsNapTime);
        Assert.Equal(14, bkr.Threshold);
        Assert.Equal(expectedStatus, bkr.Status);
    }

    [Theory]
    [InlineData(27, "green")]
    [InlineData(28, "amber")]
    [InlineData(29, "red")]
    public async Task TwoQualifiedCaregivers_NapTime_ReflectsThresholdBoundary(int presentCount, string expectedStatus)
    {
        var (client, org, location, group, deviceToken) = await SetupAsync();
        var caregiver1 = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111", "Anna");
        var caregiver2 = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2222", "Bram");
        await CheckInAsync(client, deviceToken, caregiver1.Id, "1111");
        await CheckInAsync(client, deviceToken, caregiver2.Id, "2222");

        await CheckInNNappingChildrenAsync(client, deviceToken, org.AccessToken, presentCount);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.True(bkr.IsNapTime);
        Assert.Equal(28, bkr.Threshold);
        Assert.Equal(expectedStatus, bkr.Status);
    }

    [Fact]
    public async Task ZeroQualifiedStaff_WithPresentChildren_IsBreached()
    {
        var (client, org, location, _, deviceToken) = await SetupAsync();
        await CheckInNChildrenAsync(client, deviceToken, org.AccessToken, 1);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.Equal(0, bkr.QualifiedStaffCount);
        Assert.Equal("red", bkr.Status);
    }

    [Fact]
    public async Task ZeroQualifiedStaff_ZeroPresentChildren_IsGreen()
    {
        var (client, _, location, _, deviceToken) = await SetupAsync();

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.Equal(0, bkr.PresentCount);
        Assert.Equal(0, bkr.QualifiedStaffCount);
        Assert.Equal("green", bkr.Status);
    }

    [Fact]
    public async Task StudentVolunteer_CheckedIn_DoesNotCountTowardQualifiedStaff()
    {
        var (client, org, location, _, deviceToken) = await SetupAsync();

        var staffResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Sam", "Student", $"staff_{Guid.NewGuid():N}@test.com", "+32 9 123 45 67", "StudentVolunteer", "Staff", null)));
        var student = (await staffResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        await AssignEligibilityAsync(client, org.AccessToken, student.Id, location.Id);
        await SetPinAsync(client, org.AccessToken, student.Id, "9999");
        await CheckInAsync(client, deviceToken, student.Id, "9999");

        await CheckInNChildrenAsync(client, deviceToken, org.AccessToken, 1);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.Equal(0, bkr.QualifiedStaffCount);
        Assert.Equal("red", bkr.Status);
    }

    [Fact]
    public async Task NapTime_AtLeastHalfPresentChildrenNapping_IsInferredTrue_RelaxesThreshold()
    {
        var (client, org, location, group, deviceToken) = await SetupAsync();
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        await CheckInAsync(client, deviceToken, caregiver.Id, "1234");

        // 3 present children, 2 with an open sleep event — 2*2=4 >= 3, nap time inferred.
        var childIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var child = await CreateChildAsync(client, org.AccessToken, $"NapChild{i}_{Guid.NewGuid():N}");
            await CheckInChildAsync(client, deviceToken, child.Id, Today);
            childIds.Add(child.Id);
        }

        foreach (var childId in childIds.Take(2))
            await PostChildEventAsync(client, deviceToken, childId, "sleep", DateTime.UtcNow, new { quality = (string?)null });

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.True(bkr.IsNapTime);
        Assert.Equal(14, bkr.Threshold);
    }

    [Fact]
    public async Task NapTime_FewerThanHalfPresentChildrenNapping_IsInferredFalse()
    {
        var (client, org, location, group, deviceToken) = await SetupAsync();
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        await CheckInAsync(client, deviceToken, caregiver.Id, "1234");

        // 3 present children, only 1 napping — 1*2=2 < 3, nap time not inferred.
        var childIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var child = await CreateChildAsync(client, org.AccessToken, $"NoNapChild{i}_{Guid.NewGuid():N}");
            await CheckInChildAsync(client, deviceToken, child.Id, Today);
            childIds.Add(child.Id);
        }

        await PostChildEventAsync(client, deviceToken, childIds[0], "sleep", DateTime.UtcNow, new { quality = (string?)null });

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.False(bkr.IsNapTime);
        Assert.Equal(8, bkr.Threshold);
    }

    [Fact]
    public async Task AbsentChild_ExcludedFromPresentCount()
    {
        var (client, org, location, _, deviceToken) = await SetupAsync();
        var present = await CreateChildAsync(client, org.AccessToken, "PresentChild");
        var absent = await CreateChildAsync(client, org.AccessToken, "AbsentChild");
        await CheckInChildAsync(client, deviceToken, present.Id, Today);
        await MarkAbsentAsDeviceAsync(client, deviceToken, absent.Id, location.Id, Today, justified: true);

        var bkr = await GetBkrResponseAsync(client, deviceToken, location.Id);
        Assert.Equal(1, bkr.PresentCount);
    }
}
