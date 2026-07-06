using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 2 (SC-005): assign/unassign which locations a staff member is eligible
/// to work at, independent of any day-by-day schedule.</summary>
public class StaffLocationEligibilityTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 15))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    private static async Task<StaffResponse> CreateStaffAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", accessToken,
            new CreateStaffProfileRequest("Jane", "Doe", $"staff_{Guid.NewGuid():N}@test.com", "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null))))
            .Content.ReadFromJsonAsync<StaffResponse>())!;

    // ── T038: assign to two locations ────────────────────────────────────────────

    [Fact]
    public async Task AssignEligibility_ToTwoLocations_BothAppearOnProfile()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Eligibility Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");

        var assignA = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationA.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, assignA.StatusCode);
        var assignB = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationB.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, assignB.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Contains(locationA.Id, reloaded.EligibleLocationIds);
        Assert.Contains(locationB.Id, reloaded.EligibleLocationIds);
    }

    // ── T039: unassign one of two ─────────────────────────────────────────────────

    [Fact]
    public async Task UnassignEligibility_RemovesOnlyThatLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Unassign Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");

        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationA.Id}", org.AccessToken));
        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationB.Id}", org.AccessToken));

        var unassign = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff/{staff.Id}/locations/{locationA.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.DoesNotContain(locationA.Id, reloaded.EligibleLocationIds);
        Assert.Contains(locationB.Id, reloaded.EligibleLocationIds);
    }

    // ── T040: zero eligible locations → empty list, no error ────────────────────

    [Fact]
    public async Task StaffProfile_WithZeroEligibleLocations_ReturnsEmptyListNoError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ZeroEligibility Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Empty(reloaded.EligibleLocationIds);
    }

    // ── T041: assigning a cross-tenant location id → 404 ─────────────────────────

    [Fact]
    public async Task AssignEligibility_LocationFromDifferentOrganisation_Returns404()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"CrossTenant Org A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"CrossTenant Org B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");

        var staffA = await CreateStaffAsync(client, orgA.AccessToken);
        var locationB = await CreateLocationAsync(client, orgB.AccessToken, "Org B Location");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staffA.Id}/locations/{locationB.Id}", orgA.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.location.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T075/spec.md Edge Cases: eligibility survives the location being deactivated ──

    [Fact]
    public async Task DeactivatingEligibleLocation_DoesNotAffectStaffProfileOrEligibility()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"LocationDeactivate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Soon Deactivated");

        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{location.Id}", org.AccessToken));

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Contains(location.Id, reloaded.EligibleLocationIds);
        Assert.Null(reloaded.DeactivatedAt);
    }

    // ── CHK018: removing a staff member's only remaining eligible location ──────

    [Fact]
    public async Task UnassignEligibility_OnlyRemainingLocation_ResultsInEmptyList()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"OnlyLocation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Only Location");

        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{location.Id}", org.AccessToken));
        var unassign = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/staff/{staff.Id}/locations/{location.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);

        var reloaded = (await unassign.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Empty(reloaded.EligibleLocationIds);
    }
}
