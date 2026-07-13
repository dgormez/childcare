using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Feature 008b — User Story 1 (director toggles per-location caregiver PIN requirement).
/// Mirrors LocationReservationSettingsTests' auth/registration helper pattern (013f).
/// </summary>
public class LocationCheckInSettingsTests(OrganisationOnboardingWebAppFactory factory)
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
        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
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

    // ── T006: default is "required" for a never-configured location (FR-002) ────────

    [Fact]
    public async Task GetLocation_NeverConfigured_RequiresCaregiverPinDefaultsToTrue()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("PIN Defaults Org");

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        Assert.True(reloaded.RequiresCaregiverPin);
    }

    // ── T006: update persists and doesn't leak across locations ──────────────────────

    [Fact]
    public async Task UpdateCheckInSettings_PersistsAndDoesNotAffectOtherLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Multi Loc PIN Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var location1Response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location One")));
        var location1 = (await location1Response.Content.ReadFromJsonAsync<LocationResponse>())!;
        var location2Response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location Two")));
        var location2 = (await location2Response.Content.ReadFromJsonAsync<LocationResponse>())!;

        var updateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location1.Id}/checkin-settings", org.AccessToken,
            new UpdateLocationCheckInSettingsRequest(false)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.False(updated.RequiresCaregiverPin);

        var location2Reloaded = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location2.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.True(location2Reloaded.RequiresCaregiverPin);
    }

    // ── T007: unknown location id → 404 ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateCheckInSettings_UnknownLocation_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NotFound PIN Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{Guid.NewGuid()}/checkin-settings", org.AccessToken,
            new UpdateLocationCheckInSettingsRequest(false)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.location.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T007: non-director roles get 403 ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateCheckInSettings_NonDirectorRole_Returns403()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"PIN Settings Role Org {Guid.NewGuid():N}", directorEmail);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest()));
        var location = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/checkin-settings", staffToken,
            new UpdateLocationCheckInSettingsRequest(false)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── T008b: changing the setting writes a structured, attributable log entry (FR-016) ──

    [Fact]
    public async Task UpdateCheckInSettings_ActualChange_WritesAttributableLogEntry()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("PIN Audit Org");
        factory.LogCapture.Entries.Clear();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/checkin-settings", org.AccessToken,
            new UpdateLocationCheckInSettingsRequest(false)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(factory.LogCapture.Entries, e =>
            e.Message.Contains("RequiresCaregiverPin") &&
            e.Message.Contains(location.Id.ToString()) &&
            e.Message.Contains("False") &&
            e.Message.Contains("True"));
    }

    // ── FR-016 edge case: no-op update (same value) writes no log entry ──────────────

    [Fact]
    public async Task UpdateCheckInSettings_NoActualChange_WritesNoLogEntry()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("PIN No-Op Org");
        factory.LogCapture.Entries.Clear();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/checkin-settings", org.AccessToken,
            new UpdateLocationCheckInSettingsRequest(true)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(factory.LogCapture.Entries, e => e.Message.Contains("RequiresCaregiverPin"));
    }
}
