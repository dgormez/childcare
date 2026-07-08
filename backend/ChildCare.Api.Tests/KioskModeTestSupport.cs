using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ChildCare.Api.Auth;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Shared HTTP/DB helpers for feature 008a's kiosk-mode test suite (DevicePairingTests,
/// PinManagementTests, RoomShiftTests, ShiftAttributionServiceTests, DeviceTokenRotationTests).
/// Pairing a device, provisioning an eligible caregiver with a PIN, and reaching into the
/// tenant schema directly all repeat identically across every one of those files — unlike the
/// trivial single-line helpers other test classes in this project duplicate per-class, this is
/// substantial enough (device pairing alone is a 3-call sequence) to warrant one shared home.
/// </summary>
internal static class KioskModeTestSupport
{
    public static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest(email)),
        };
        request.Headers.Add("X-Superadmin-Key", OrganisationOnboardingWebAppFactory.SuperAdminApiKey);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;
    }

    public static async Task<RegisterOrganisationResponse> RegisterOrgAsync(HttpClient client, string orgName, string email)
    {
        var invitation = await CreateInvitationAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, orgName, $"{orgName} Director", email, "password123"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;
    }

    public static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    public static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 15))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    public static async Task<GroupResponse> CreateGroupAsync(HttpClient client, string accessToken, string name, Guid locationId) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/groups", accessToken, new CreateGroupRequest(name, locationId))))
            .Content.ReadFromJsonAsync<GroupResponse>())!;

    public static async Task<StaffResponse> CreateStaffAsync(HttpClient client, string accessToken, string firstName = "Jane") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", accessToken,
            new CreateStaffProfileRequest(firstName, "Doe", $"staff_{Guid.NewGuid():N}@test.com", "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null))))
            .Content.ReadFromJsonAsync<StaffResponse>())!;

    public static async Task AssignEligibilityAsync(HttpClient client, string accessToken, Guid staffId, Guid locationId)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staffId}/locations/{locationId}", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static async Task UnassignEligibilityAsync(HttpClient client, string accessToken, Guid staffId, Guid locationId)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff/{staffId}/locations/{locationId}", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static async Task<HttpResponseMessage> SetPinRawAsync(HttpClient client, string accessToken, Guid staffId, string pin) =>
        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staffId}/pin", accessToken, new SetCaregiverPinRequest(pin)));

    public static async Task SetPinAsync(HttpClient client, string accessToken, Guid staffId, string pin)
    {
        var response = await SetPinRawAsync(client, accessToken, staffId, pin);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>Creates a caregiver eligible at <paramref name="locationId"/> with the given PIN
    /// in one step — the common setup every check-in/out/confirm test needs.</summary>
    public static async Task<StaffResponse> CreateEligibleCaregiverWithPinAsync(
        HttpClient client, string accessToken, Guid locationId, string pin, string firstName = "Jane")
    {
        var staff = await CreateStaffAsync(client, accessToken, firstName);
        await AssignEligibilityAsync(client, accessToken, staff.Id, locationId);
        await SetPinAsync(client, accessToken, staff.Id, pin);
        return staff;
    }

    /// <summary>Pairs a fresh device to the given location/group as director, returning the
    /// device id plus a bearer-ready device token — every room-shift/device test authenticates
    /// with this token instead of a user JWT (research.md R1).</summary>
    public static async Task<(Guid DeviceId, string DeviceToken)> PairDeviceAsync(
        HttpClient client, string accessToken, Guid locationId, Guid groupId, string overridePin = "135790")
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/devices/pair", accessToken,
            new { locationId, groupId, directorOverridePin = overridePin }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pairing = (await response.Content.ReadFromJsonAsync<DevicePairingResponse>())!;
        return (pairing.DeviceId, pairing.DeviceToken);
    }

    public static HttpRequestMessage DeviceRequest(HttpMethod method, string url, string deviceToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    public static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string deviceToken, Guid staffId, string pin) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/room-shifts/check-in", deviceToken, new { staffId, pin }));

    public static Task<HttpResponseMessage> CheckOutAsync(HttpClient client, string deviceToken, Guid staffId, string pin) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/room-shifts/check-out", deviceToken, new { staffId, pin }));

    public static Task<HttpResponseMessage> GetRosterAsync(HttpClient client, string deviceToken) =>
        client.SendAsync(DeviceRequest(HttpMethod.Get, "/api/room-shifts/roster", deviceToken));

    public static Task<HttpResponseMessage> ConfirmAdministratorAsync(HttpClient client, string deviceToken, Guid? staffId, string? pin, bool skip) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/room-shifts/confirm-administrator", deviceToken, new { staffId, pin, skip }));

    public static async Task<string> GetSchemaNameAsync(IServiceProvider services, Guid tenantId)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    public static ITenantDbContext ResolveTenantDb(IServiceProvider services, string schemaName) =>
        services.GetRequiredService<ITenantDbContextResolver>().ForSchema(schemaName);

    /// <summary>Hand-mints an already-expired device token (signed with the same config the app
    /// itself validates against) — used to prove real JWT-lifetime expiry is rejected, since
    /// there's no way to make a `PairDeviceAsync`-issued token's already-embedded `exp` claim
    /// retroactively expire by mutating the DB afterward (research.md R3, T071).</summary>
    public static string IssueExpiredDeviceToken(
        IConfiguration config, Guid tenantId, Guid deviceId, Guid locationId, Guid groupId, int tokenVersion = 1)
    {
        var secret = config["DeviceJwt:Secret"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(DeviceTokenClaims.TenantId, tenantId.ToString()),
            new Claim(DeviceTokenClaims.DeviceId, deviceId.ToString()),
            new Claim(DeviceTokenClaims.LocationId, locationId.ToString()),
            new Claim(DeviceTokenClaims.GroupId, groupId.ToString()),
            new Claim(DeviceTokenClaims.TokenVersion, tokenVersion.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["DeviceJwt:Issuer"],
            audience: config["DeviceJwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow.AddDays(-31),
            expires: DateTime.UtcNow.AddDays(-1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
