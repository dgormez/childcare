using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>User Story 1 (feature 008a): a director pairs a tablet to a location/group, exits
/// room mode via the director-override PIN, and can revoke a lost/stolen tablet. Also covers
/// User Story 4's cross-location rejection (T059) and User Story 7's revocation (T075/T076).</summary>
public class DevicePairingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T017: pairing issues a device token immediately usable against a DeviceAuthenticated endpoint ──

    [Fact]
    public async Task PairDevice_IssuesDeviceToken_ImmediatelyUsableAgainstDeviceAuthenticatedEndpoint()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Pairing Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);

        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        Assert.False(string.IsNullOrEmpty(deviceToken));

        var rosterRequest = new HttpRequestMessage(HttpMethod.Get, "/api/room-shifts/roster");
        rosterRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var rosterResponse = await client.SendAsync(rosterRequest);
        Assert.Equal(HttpStatusCode.OK, rosterResponse.StatusCode);
    }

    // ── T018: exit-room-mode correct override PIN succeeds; incorrect fails without touching
    //    caregiver-PIN lockout; 10 incorrect attempts locks the device for 30 minutes ──

    [Fact]
    public async Task ExitRoomMode_CorrectOverridePin_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ExitRoom Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id, "246810");

        var exitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/devices/exit-room-mode")
        {
            Content = JsonContent.Create(new { directorOverridePin = "246810" }),
        };
        exitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var exitResponse = await client.SendAsync(exitRequest);
        Assert.Equal(HttpStatusCode.OK, exitResponse.StatusCode);
    }

    [Fact]
    public async Task ExitRoomMode_IncorrectOverridePin_Fails_WithoutTouchingCaregiverPinLockout()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ExitRoomBad Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id, "246810");

        var exitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/devices/exit-room-mode")
        {
            Content = JsonContent.Create(new { directorOverridePin = "000000" }),
        };
        exitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var exitResponse = await client.SendAsync(exitRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, exitResponse.StatusCode);
        Assert.Contains("errors.devices.invalid_override_pin", await exitResponse.Content.ReadAsStringAsync());

        // The caregiver's own PIN lockout is untouched by a wrong override-PIN attempt —
        // check-in with the correct caregiver PIN still succeeds immediately after.
        var checkInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/room-shifts/check-in")
        {
            Content = JsonContent.Create(new { staffId = staff.Id, pin = "1234" }),
        };
        checkInRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var checkInResponse = await client.SendAsync(checkInRequest);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);
    }

    [Fact]
    public async Task ExitRoomMode_TenIncorrectAttempts_Locks423ForThirtyMinutes()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ExitRoomLock Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id, "246810");

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 10; i++)
        {
            var exitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/devices/exit-room-mode")
            {
                Content = JsonContent.Create(new { directorOverridePin = "000000" }),
            };
            exitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
            lastResponse = await client.SendAsync(exitRequest);
        }

        Assert.Equal(HttpStatusCode.Locked, lastResponse!.StatusCode);
        Assert.Contains("errors.devices.override_pin_locked", await lastResponse.Content.ReadAsStringAsync());
    }

    // ── T020: exiting room mode auto-closes any RoomShift still open under the tablet's DevicePairingId ──

    [Fact]
    public async Task ExitRoomMode_AutoClosesOpenShifts_WithReassignedReason()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ExitReassign Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (deviceId, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id, "246810");

        var checkInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/room-shifts/check-in")
        {
            Content = JsonContent.Create(new { staffId = staff.Id, pin = "1234" }),
        };
        checkInRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(checkInRequest)).StatusCode);

        var exitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/devices/exit-room-mode")
        {
            Content = JsonContent.Create(new { directorOverridePin = "246810" }),
        };
        exitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(exitRequest)).StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var shift = await db.RoomShifts.SingleAsync(s => s.DevicePairingId == deviceId && s.StaffProfileId == staff.Id);
        Assert.NotNull(shift.CheckedOutAt);
        Assert.Equal("reassigned", shift.ClosedReason);
    }

    // ── T059 (US4): a staffId eligible only at a different location is rejected 403, regardless of PIN correctness ──

    [Fact]
    public async Task CheckIn_StaffEligibleOnlyAtDifferentLocation_Returns403_RegardlessOfPinCorrectness()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CrossLocation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", locationB.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, locationA.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id);

        var checkInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/room-shifts/check-in")
        {
            Content = JsonContent.Create(new { staffId = staff.Id, pin = "1234" }),
        };
        checkInRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var response = await client.SendAsync(checkInRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.staff.not_eligible_here", await response.Content.ReadAsStringAsync());
    }

    // ── T075 (US7): revoking a tablet causes its very next request to fail 401, independent of remaining TTL ──

    [Fact]
    public async Task RevokeDevice_VeryNextRequest_Fails401_Revoked()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Revoke Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (deviceId, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var revokeResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/devices/{deviceId}/revoke", org.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var rosterRequest = new HttpRequestMessage(HttpMethod.Get, "/api/room-shifts/roster");
        rosterRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var rosterResponse = await client.SendAsync(rosterRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, rosterResponse.StatusCode);
        Assert.Contains("errors.devices.revoked", await rosterResponse.Content.ReadAsStringAsync());
    }

    // ── T076 (US7): a since-revoked device's rejected request (e.g. an offline-queue sync replay) is audit-logged ──

    [Fact]
    public async Task RevokedDevice_RejectedRequest_IsLoggedServerSide()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"RevokeAudit Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (deviceId, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/devices/{deviceId}/revoke", org.AccessToken));

        // Simulates a queued offline check-in replaying against a since-revoked device token.
        var checkInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/room-shifts/check-in")
        {
            Content = JsonContent.Create(new { staffId = Guid.NewGuid(), pin = "0000" }),
        };
        checkInRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        var response = await client.SendAsync(checkInRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Assert.Contains(factory.LogCapture.Entries, e =>
            e.Message.Contains("Rejected request from revoked or unknown device", StringComparison.Ordinal) &&
            e.Message.Contains(deviceId.ToString(), StringComparison.Ordinal));
    }
}
