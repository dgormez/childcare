using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffScheduling;

/// <summary>
/// research.md R1: feature 010's live BKR ratio (`GetBkrRatioQuery`) is sourced from
/// `RoomShifts` real-time check-in presence, never from `staff_schedules`. This is the
/// regression test proving feature 012's data (specifically, marking a checked-in caregiver
/// absent in their planned rota) has zero effect on the live BKR computation — the two stay
/// decoupled, as designed.
/// </summary>
public class BkrDecouplingTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

    [Fact]
    public async Task MarkingCheckedInCaregiverAbsentInRota_DoesNotChangeLiveBkrRatio()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Bkr Decoupling Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");

        var checkInResponse = await CheckInAsync(client, deviceToken, caregiver.Id, "1234");
        Assert.Equal(System.Net.HttpStatusCode.OK, checkInResponse.StatusCode);

        var bkrBefore = (await (await GetBkrAsync(client, deviceToken, location.Id)).Content.ReadFromJsonAsync<BkrRatioResponse>())!;
        Assert.Equal(1, bkrBefore.QualifiedStaffCount);

        // Plan the same caregiver into feature 012's rota and mark them absent — this touches
        // only `staff_schedules`, never `RoomShifts`.
        var entryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-schedules", org.AccessToken,
            new CreateStaffScheduleRequest(caregiver.Id, location.Id, group.Id, FutureDate, new TimeOnly(8, 0), new TimeOnly(16, 0))));
        var entry = (await entryResponse.Content.ReadFromJsonAsync<StaffScheduleResponse>())!;
        var absenceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-schedules/{entry.Id}/absence", org.AccessToken,
            new MarkAbsenceRequest(true, "sick")));
        Assert.Equal(System.Net.HttpStatusCode.OK, absenceResponse.StatusCode);

        var bkrAfter = (await (await GetBkrAsync(client, deviceToken, location.Id)).Content.ReadFromJsonAsync<BkrRatioResponse>())!;
        Assert.Equal(bkrBefore.QualifiedStaffCount, bkrAfter.QualifiedStaffCount);
        Assert.Equal(1, bkrAfter.QualifiedStaffCount);
    }
}
