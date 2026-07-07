using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 1 (feature 008): a caregiver looks up their own staff profile and
/// eligible locations via a self-service endpoint, since every other /api/staff route remains
/// DirectorOnly.</summary>
public class StaffMeTests(OrganisationOnboardingWebAppFactory factory)
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

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static string ExtractLatestStaffInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name = "Main Building") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    /// <summary>Creates a caregiver, accepts their invitation, assigns them to the given
    /// location(s), and returns their own access token.</summary>
    private static async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
        HttpClient client, OrganisationOnboardingWebAppFactory factory, string orgSlug, string directorAccessToken, params Guid[] locationIds)
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        foreach (var locationId in locationIds)
            await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationId}", directorAccessToken));

        var token = ExtractLatestStaffInviteToken(factory, email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    // ── T020: GET /api/staff/me returns own profile + eligible locations ────────

    [Fact]
    public async Task GetStaffMe_AsCaregiver_ReturnsOwnProfileAndEligibleLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Me Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var (staff, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, location.Id);

        var meResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff/me", caregiverToken));
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = (await meResponse.Content.ReadFromJsonAsync<StaffMeResponse>())!;

        Assert.Equal(staff.Id, me.StaffProfileId);
        Assert.Equal("staff", me.Role);
        Assert.Contains(location.Id, me.EligibleLocationIds);
    }

    // ── A director can also call it (StaffOrDirector) ────────────────────────────

    [Fact]
    public async Task GetStaffMe_AsDirectorWithNoOwnStaffProfile_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Me Director Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        // The registering director has no StaffProfile of their own (only a TenantUser) unless
        // one is separately created — confirms GetStaffMeQuery reports NotFound cleanly rather
        // than throwing when no StaffProfile row exists for the caller.
        var meResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff/me", org.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, meResponse.StatusCode);
        Assert.Contains("errors.staff.profile_not_found", await meResponse.Content.ReadAsStringAsync());
    }
}
