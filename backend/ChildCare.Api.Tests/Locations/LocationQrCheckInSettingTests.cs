using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Locations;

/// <summary>
/// Feature 021 — User Story 1 (director toggles the per-location QR check-in setting).
/// Mirrors LocationCheckInSettingsTests' auth/registration helper pattern (008b) — a sibling
/// setting on the same Location entity, not a reuse of that feature's endpoint/command.
/// </summary>
public class LocationQrCheckInSettingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient client, string email)
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

    private static async Task<RegisterOrganisationResponse> RegisterOrgAsync(HttpClient client, string orgName, string email)
    {
        var invitation = await CreateInvitationAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, orgName, $"{orgName} Director", email, "password123"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;
    }

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    private async Task InsertUserWithRoleAsync(string schemaName, string email, string password, UserRole role)
    {
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        db.Users.Add(new Domain.Entities.TenantUser
        {
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Name         = $"Test {role}",
            Role         = role,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client, string slug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return body.AccessToken;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static CreateLocationRequest DefaultCreateRequest(string name = "Main Building") =>
        new(name, "123 Kerkstraat, 9000 Gent", "+32 9 123 45 67", $"{Guid.NewGuid():N}@location.test", 20);

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> CreateOrgWithLocationAsync(string orgLabel)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"{orgLabel} {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest()));
        var location = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        return (client, org, location);
    }

    // ── T014: default is disabled for a never-configured location (FR-002/SC-002) ────

    [Fact]
    public async Task GetLocation_NeverConfigured_QrCheckInEnabledDefaultsToFalse()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("QR Defaults Org");

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        Assert.False(reloaded.QrCheckInEnabled);
    }

    // ── T011: update persists and doesn't leak across locations ──────────────────────

    [Fact]
    public async Task UpdateQrCheckInSetting_PersistsAndDoesNotAffectOtherLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Multi Loc QR Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var location1Response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location One")));
        var location1 = (await location1Response.Content.ReadFromJsonAsync<LocationResponse>())!;
        var location2Response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location Two")));
        var location2 = (await location2Response.Content.ReadFromJsonAsync<LocationResponse>())!;

        var updateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location1.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(true)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.True(updated.QrCheckInEnabled);

        var location2Reloaded = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location2.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.False(location2Reloaded.QrCheckInEnabled);
    }

    // ── T013: unknown location id → 404 ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateQrCheckInSetting_UnknownLocation_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NotFound QR Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{Guid.NewGuid()}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(true)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.location.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T013: non-director roles get 403 ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateQrCheckInSetting_NonDirectorRole_Returns403()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"QR Settings Role Org {Guid.NewGuid():N}", directorEmail);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest()));
        var location = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", staffToken,
            new UpdateLocationQrCheckInSettingRequest(true)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── T012: changing the setting writes a structured, attributable log entry (FR-016) ──

    [Fact]
    public async Task UpdateQrCheckInSetting_ActualChange_WritesAttributableLogEntry()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("QR Audit Org");
        factory.LogCapture.Entries.Clear();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(true)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(factory.LogCapture.Entries, e =>
            e.Message.Contains("QrCheckInEnabled") &&
            e.Message.Contains(location.Id.ToString()) &&
            e.Message.Contains("False") &&
            e.Message.Contains("True"));
    }

    // ── T012 edge case: no-op update (same value) writes no log entry ────────────────

    [Fact]
    public async Task UpdateQrCheckInSetting_NoActualChange_WritesNoLogEntry()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("QR No-Op Org");
        factory.LogCapture.Entries.Clear();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(false)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(factory.LogCapture.Entries, e => e.Message.Contains("QrCheckInEnabled"));
    }

    // ── T013a (FR-015): toggling the setting never touches an existing AttendanceRecord ──

    [Fact]
    public async Task UpdateQrCheckInSetting_DoesNotAffectExistingAttendanceRecords()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"QR Attendance Unaffected Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = BelgianCalendarDay.Today();
        var checkInResponse = await CheckInChildAsync(client, deviceToken, child.Id, today);
        var original = (await checkInResponse.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        var enableResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(true)));
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

        var disableResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(false)));
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        var listResponse = await ListAttendanceAsync(client, org.AccessToken, location.Id, today);
        var records = (await listResponse.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        var reloaded = Assert.Single(records.Items, r => r.Id == original.Id);

        Assert.Equal(original.Status, reloaded.Status);
        Assert.NotNull(original.CheckInAt);
        Assert.NotNull(reloaded.CheckInAt);
        Assert.True(
            (reloaded.CheckInAt.Value - original.CheckInAt.Value).Duration() < TimeSpan.FromMilliseconds(1),
            $"Expected check-in timestamp to remain unchanged, got {reloaded.CheckInAt:O} instead of {original.CheckInAt:O}");
        Assert.Equal(original.CheckOutAt, reloaded.CheckOutAt);
        Assert.Equal(original.PlannedDurationMinutes, reloaded.PlannedDurationMinutes);
        Assert.Equal(original.RecordedBy, reloaded.RecordedBy);
    }
}
