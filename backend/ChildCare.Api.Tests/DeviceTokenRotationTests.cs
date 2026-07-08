using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>User Story 6 (feature 008a): a device token nearing expiry rotates silently via
/// the X-Device-Token-Refresh response header, without breaking an offline-queue replay burst
/// still carrying the pre-rotation token (T069/T070). A fully expired token is rejected
/// distinctly from a revoked one (T071 — T074's revoked half is DevicePairingTests.
/// RevokeDevice_VeryNextRequest_Fails401_Revoked / ShiftAttributionServiceTests.
/// RoomShiftsRoster_ExpiredDeviceToken_Rejected401_TokenExpired covers the expired half).</summary>
public class DeviceTokenRotationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T069: a token with < 7 days remaining triggers X-Device-Token-Refresh on the next
    //    authenticated request, and DevicePairing.TokenVersion increments ──

    [Fact]
    public async Task NearExpiryToken_TriggersRotationHeader_AndIncrementsTokenVersion()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Rotation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (deviceId, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var pairing = await db.DevicePairings.SingleAsync(d => d.Id == deviceId);
        pairing.TokenIssuedAt = DateTime.UtcNow.AddDays(-24); // 30-day TTL, 6 days remaining < 7-day threshold
        await db.SaveChangesAsync();

        var response = await GetRosterAsync(client, deviceToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Device-Token-Refresh"));
        var newToken = response.Headers.GetValues("X-Device-Token-Refresh").Single();
        Assert.False(string.IsNullOrEmpty(newToken));

        // A fresh context, not the one used above to write the mutation — reusing the same
        // tracked instance would return EF Core's stale in-memory copy instead of re-reading
        // what the endpoint filter (a separate DbContext, inside the request) actually wrote.
        var reloaded = await ResolveTenantDb(factory.Services, schema).DevicePairings.SingleAsync(d => d.Id == deviceId);
        Assert.Equal(2, reloaded.TokenVersion);

        // The newly issued token is itself immediately usable.
        var newTokenResponse = await GetRosterAsync(client, newToken);
        Assert.Equal(HttpStatusCode.OK, newTokenResponse.StatusCode);
    }

    // ── T070: a batch of requests already carrying the pre-rotation token all still succeed ──

    [Fact]
    public async Task BurstOfRequestsWithPreRotationToken_AllStillSucceed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"RotationBurst Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (deviceId, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var pairing = await db.DevicePairings.SingleAsync(d => d.Id == deviceId);
        pairing.TokenIssuedAt = DateTime.UtcNow.AddDays(-24);
        await db.SaveChangesAsync();

        // First request triggers rotation (TokenVersion 1 -> 2) — simulating an offline queue
        // that hasn't swapped its stored token yet, every subsequent replayed request in the
        // burst still carries the original (now pre-rotation) token and must still succeed.
        for (var i = 0; i < 5; i++)
        {
            var response = await GetRosterAsync(client, deviceToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ── T071: a fully expired (30-day) token is rejected 401 device.token_expired ──

    [Fact]
    public async Task FullyExpiredToken_Rejected401_TokenExpired_DistinctFromRevoked()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"FullyExpired Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (deviceId, _) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        // A token's own `exp` claim is fixed at mint time — a 30-day-old DevicePairing row
        // doesn't retroactively expire an already-issued JWT, so this hand-mints one whose
        // `exp` claim is already in the past instead (same technique as ShiftAttributionServiceTests).
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = IssueExpiredDeviceToken(config, org.Organisation.Id, deviceId, location.Id, group.Id);

        var response = await GetRosterAsync(client, expiredToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("errors.devices.token_expired", body);
        Assert.DoesNotContain("errors.devices.revoked", body);
    }
}
