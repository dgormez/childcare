using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// Feature 013f — User Story 1 (director configures per-location reservation policy) and
/// User Story 4 (pending-requests warning before a mode change strands them). Tests reuse
/// LocationCrudTests' auth/registration helper pattern.
/// </summary>
public class LocationReservationSettingsTests(OrganisationOnboardingWebAppFactory factory)
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

    // ── T010: defaults on a never-configured location ────────────────────────────

    [Fact]
    public async Task GetLocation_NeverConfigured_ReturnsColumnDefaults()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("Defaults Org");

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        Assert.Equal("approval", reloaded.ReservationAbsencesMode);
        Assert.Equal("approval", reloaded.ReservationExtrasMode);
        Assert.Equal("disabled", reloaded.ReservationSwapsMode);
        Assert.Equal(0, reloaded.ReservationNoticeHours);
    }

    // ── T011: update persists and doesn't leak across locations ──────────────────

    [Fact]
    public async Task UpdateReservationSettings_PersistsAndDoesNotAffectOtherLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Multi Loc Settings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var location1Response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location One")));
        var location1 = (await location1Response.Content.ReadFromJsonAsync<LocationResponse>())!;
        var location2Response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location Two")));
        var location2 = (await location2Response.Content.ReadFromJsonAsync<LocationResponse>())!;

        var updateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location1.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("informational", "disabled", "approval", 24, false)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("informational", updated.ReservationAbsencesMode);
        Assert.Equal("disabled", updated.ReservationExtrasMode);
        Assert.Equal("approval", updated.ReservationSwapsMode);
        Assert.Equal(24, updated.ReservationNoticeHours);

        var location2Reloaded = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location2.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("approval", location2Reloaded.ReservationAbsencesMode);
        Assert.Equal("approval", location2Reloaded.ReservationExtrasMode);
        Assert.Equal("disabled", location2Reloaded.ReservationSwapsMode);
        Assert.Equal(0, location2Reloaded.ReservationNoticeHours);
    }

    // ── T012: invalid mode / out-of-range notice hours → 422 ─────────────────────

    [Theory]
    [InlineData("not-a-mode", "approval", "disabled", 0)]
    [InlineData("approval", "not-a-mode", "disabled", 0)]
    [InlineData("approval", "approval", "not-a-mode", 0)]
    [InlineData("approval", "approval", "disabled", -1)]
    [InlineData("approval", "approval", "disabled", 8761)]
    public async Task UpdateReservationSettings_InvalidInput_Returns422(string absences, string extras, string swaps, int noticeHours)
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("Invalid Settings Org");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest(absences, extras, swaps, noticeHours, false)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── T013: unknown location id → 404 ───────────────────────────────────────────

    [Fact]
    public async Task UpdateReservationSettings_UnknownLocation_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NotFound Settings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{Guid.NewGuid()}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("approval", "approval", "disabled", 0, false)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.location.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T014: non-director roles get 403 ──────────────────────────────────────────

    [Fact]
    public async Task UpdateReservationSettings_NonDirectorRole_Returns403()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Settings Role Org {Guid.NewGuid():N}", directorEmail);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest()));
        var location = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/reservation-settings", staffToken,
            new UpdateLocationReservationSettingsRequest("approval", "approval", "disabled", 0, false)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── User Story 4: pending-requests warning before a mode change strands them (FR-014) ────

    private static readonly DateOnly Monday = new(2027, 6, 7);

    [Fact]
    public async Task ChangeModeAwayFromApproval_WithPendingRequests_Returns409WithCounts_AndDoesNotPersist()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("Pending Warning Org");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var submitResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/day-reservations", parentToken,
            new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null)));
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("disabled", "approval", "disabled", 0, false)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("errors.location.reservation_settings.pending_requests_warning", body);
        Assert.Contains("\"absence\":1", body);

        var reloaded = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("approval", reloaded.ReservationAbsencesMode);
    }

    [Fact]
    public async Task ChangeModeAwayFromApproval_ConfirmDespitePending_PersistsAndLeavesPendingRequestUntouched()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("Pending Confirm Org");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var submitResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/day-reservations", parentToken,
            new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null)));
        var reservation = (await submitResponse.Content.ReadFromJsonAsync<DayReservationResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("disabled", "approval", "disabled", 0, true)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("disabled", updated.ReservationAbsencesMode);

        var pendingListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/day-reservations?status=pending", org.AccessToken));
        var pendingList = (await pendingListResponse.Content.ReadFromJsonAsync<List<DayReservationResponse>>())!;
        Assert.Contains(pendingList, r => r.Id == reservation.Id);
    }

    [Fact]
    public async Task ChangeModeAwayFromApproval_NoPendingRequests_SavesDirectlyWithoutWarning()
    {
        var (client, org, location) = await CreateOrgWithLocationAsync("No Pending Org");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("disabled", "approval", "disabled", 0, false)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
