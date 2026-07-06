using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 4 (SC-004): a staff member can be deactivated (soft-delete, blocks
/// login, hides from active rosters, never hard-deleted) and reactivated. Also covers
/// FR-010's refresh-token invalidation and the Director-vs-Staff distinction
/// (/speckit-checklist CHK006/CHK016).</summary>
public class StaffDeactivationTests(OrganisationOnboardingWebAppFactory factory)
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

    /// <summary>Creates a staff profile, accepts its invitation, and logs in — returning a
    /// fully-onboarded staff account's profile plus a working session.</summary>
    private async Task<(StaffResponse Staff, AuthSessionResponse Session)> CreateOnboardedStaffAsync(
        HttpClient client, RegisterOrganisationResponse org)
    {
        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Jane", "Doe", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var token = ExtractLatestStaffInviteToken(factory, email);
        var acceptResponse = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "password123"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session);
    }

    // ── T053: deactivate excludes from default list, includeDeactivated shows it ─

    [Fact]
    public async Task Deactivate_ExcludesFromDefaultList_ButVisibleWithIncludeDeactivated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Deactivation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, _) = await CreateOnboardedStaffAsync(client, org);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = (await deactivateResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.NotNull(deactivated.DeactivatedAt);

        var defaultListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff", org.AccessToken));
        var defaultList = (await defaultListResponse.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.DoesNotContain(defaultList, s => s.Id == staff.Id);

        var includeDeactivatedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff?includeDeactivated=true", org.AccessToken));
        var includeDeactivatedList = (await includeDeactivatedResponse.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.Contains(includeDeactivatedList, s => s.Id == staff.Id);
    }

    // ── T054: deactivated staff member's login fails cleanly ────────────────────

    [Fact]
    public async Task Deactivate_ThenLogin_FailsWithInvalidCredentials()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DeactivateLogin Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var email = $"staff_{Guid.NewGuid():N}@test.com";

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Jane", "Doe", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var token = ExtractLatestStaffInviteToken(factory, email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "password123"));

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.Contains("errors.auth.invalid_credentials", await loginResponse.Content.ReadAsStringAsync());
    }

    // ── T055: reactivate restores login ──────────────────────────────────────────

    [Fact]
    public async Task Reactivate_RestoresLogin()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Reactivation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, _) = await CreateOnboardedStaffAsync(client, org);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));

        var reactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        var reactivated = (await reactivateResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Null(reactivated.DeactivatedAt);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email = staff.Email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    // ── T056: idempotent deactivate/reactivate ───────────────────────────────────

    [Fact]
    public async Task DeactivateAndReactivate_AreIdempotent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Idempotent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, _) = await CreateOnboardedStaffAsync(client, org);

        var firstDeactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));
        var secondDeactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstDeactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondDeactivate.StatusCode);

        var firstReactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/reactivate", org.AccessToken));
        var secondReactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstReactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondReactivate.StatusCode);

        var final = (await secondReactivate.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Null(final.DeactivatedAt);
    }

    // ── T057: deactivated profile still returns full details (name never hidden) ─

    [Fact]
    public async Task DeactivatedProfile_StillReturnsFullDetailsIncludingName()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff History Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, _) = await CreateOnboardedStaffAsync(client, org);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}?includeDeactivated=true", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Equal("Jane", reloaded.FirstName);
        Assert.Equal("Doe", reloaded.LastName);
    }

    // ── CHK016: deactivating a director's own Staff Profile never blocks login ──

    [Fact]
    public async Task DeactivateDirectorsOwnStaffProfile_DoesNotBlockDirectorLogin()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Director OptIn Deactivate Org {Guid.NewGuid():N}", directorEmail);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Director", "Self", "unused@test.com", "+32 9 000 00 00", "QualifiedCaregiver", "Director", org.Director.Id)));
        var directorStaffProfile = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{directorStaffProfile.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.DoesNotContain(list, s => s.Id == directorStaffProfile.Id);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email = directorEmail, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    // ── FR-010/SC-004: deactivation invalidates existing refresh tokens ─────────

    [Fact]
    public async Task Deactivate_InvalidatesExistingRefreshToken()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"RefreshInvalidate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (staff, session) = await CreateOnboardedStaffAsync(client, org);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = session.RefreshToken });
        Assert.NotEqual(HttpStatusCode.OK, refreshResponse.StatusCode);
    }
}
