using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>Device-readable "today's attendance at my location" lookup — the caregiver tablet's
/// group view needs this to render current check-in/absence state.</summary>
public class GetTodayAttendanceTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetToday_ReturnsRecordsForDevicesOwnLocation_Today()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Today Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = BelgianCalendarDay.Today();
        await CheckInChildAsync(client, deviceToken, child.Id, today);

        var response = await client.SendAsync(KioskModeTestSupport.DeviceRequest(HttpMethod.Get, "/api/attendance/today", deviceToken));
        var records = (await response.Content.ReadFromJsonAsync<List<AttendanceRecordResponse>>())!;

        Assert.Single(records, r => r.ChildId == child.Id && r.Status == "present");
    }

    [Fact]
    public async Task GetToday_ExcludesOtherLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Today Other Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceTokenA) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = BelgianCalendarDay.Today();
        await CheckInChildAsync(client, deviceTokenA, child.Id, today);

        var otherGroup = await CreateGroupAsync(client, org.AccessToken, "Group B", otherLocation.Id);
        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, otherLocation.Id, otherGroup.Id);
        var response = await client.SendAsync(KioskModeTestSupport.DeviceRequest(HttpMethod.Get, "/api/attendance/today", deviceTokenB));
        var records = (await response.Content.ReadFromJsonAsync<List<AttendanceRecordResponse>>())!;

        Assert.Empty(records);
    }
}
