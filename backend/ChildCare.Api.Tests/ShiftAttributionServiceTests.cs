using System.Net;
using System.Net.Http.Headers;
using ChildCare.Application.RoomShifts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 4 (feature 008a): proves the device-token-sufficiency claim end-to-end rather
/// than merely asserting it in the spec — T056 (attribution resolves correctly for 0/1/2
/// concurrently checked-in caregivers), T057 (a device-authenticated write never gates on
/// individual HTTP auth), and T058 (every DeviceAuthenticated endpoint rejects a missing or
/// expired token, independent of shift-log state — the revoked-token half of T058 is
/// DevicePairingTests.RevokeDevice_VeryNextRequest_Fails401_Revoked).
/// </summary>
public class ShiftAttributionServiceTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T056: ResolveRecordedByAsync returns [], [A], [A, B] as caregivers check in/out ──

    [Fact]
    public async Task ResolveRecordedByAsync_ReturnsEmptyThenA_ThenBothAB()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Attribution Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staffA = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111", "Alice");
        var staffB = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2222", "Bob");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var service = new ShiftAttributionService(ResolveTenantDb(factory.Services, schema));
        var now = DateTime.UtcNow;

        Assert.Empty(await service.ResolveRecordedByAsync(location.Id, group.Id, now));

        await CheckInAsync(client, deviceToken, staffA.Id, "1111");
        var afterA = await service.ResolveRecordedByAsync(location.Id, group.Id, DateTime.UtcNow);
        Assert.Equal(new[] { staffA.Id }, afterA);

        await CheckInAsync(client, deviceToken, staffB.Id, "2222");
        var afterBoth = await service.ResolveRecordedByAsync(location.Id, group.Id, DateTime.UtcNow);
        Assert.Equal(new HashSet<Guid> { staffA.Id, staffB.Id }, afterBoth.ToHashSet());
    }

    // ── T057: check-in succeeds via device-token alone, with zero *other* caregivers checked in ──

    [Fact]
    public async Task CheckIn_DeviceTokenAlone_SucceedsWithZeroOtherCaregiversCheckedIn()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SoloCheckIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CheckInAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Feature 008b (T031): attribution resolves identically for a PIN-off-created shift ──

    [Fact]
    public async Task ResolveRecordedByAsync_PinOffLocation_ResolvesIdenticallyToPinOnLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Attribution Parity Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await SetRequiresCaregiverPinAsync(client, org.AccessToken, location.Id, false);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var service = new ShiftAttributionService(ResolveTenantDb(factory.Services, schema));

        await CheckInAsync(client, deviceToken, staff.Id, null);
        var recordedBy = await service.ResolveRecordedByAsync(location.Id, group.Id, DateTime.UtcNow);

        Assert.Equal(new[] { staff.Id }, recordedBy);
    }

    // ── T058: DeviceAuthenticated endpoints reject a missing or expired token ──

    [Fact]
    public async Task RoomShiftsRoster_NoDeviceToken_Rejected401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/room-shifts/roster");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RoomShiftsRoster_ExpiredDeviceToken_Rejected401_TokenExpired()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ExpiredToken Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (deviceId, _) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var config = factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = IssueExpiredDeviceToken(config, org.Organisation.Id, deviceId, location.Id, group.Id);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/room-shifts/roster");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("errors.devices.token_expired", await response.Content.ReadAsStringAsync());
    }

}
