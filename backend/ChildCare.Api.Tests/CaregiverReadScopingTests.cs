using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 2 (feature 008, research.md R6): a caregiver's reads of `GET /api/children`,
/// `GET /api/children/{id}`, and `GET /api/groups` are scoped to the location(s) they're
/// eligible for, while a director's view of the same endpoints remains fully unfiltered
/// (feature 006 behavior unchanged).
/// </summary>
public class CaregiverReadScopingTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient client, string accessToken, Guid locationId, string name = "Baby Room") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/groups", accessToken, new CreateGroupRequest(name, locationId))))
            .Content.ReadFromJsonAsync<GroupResponse>())!;

    private static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, new DateOnly(2026, 1, 1))));

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

    // ── T033: GET /api/groups scoped to caregiver's eligible location(s) ────────

    [Fact]
    public async Task GetGroups_AsCaregiver_OnlyReturnsGroupsAtEligibleLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Scoping Groups Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, locationA.Id, "Group A");
        var groupB = await CreateGroupAsync(client, org.AccessToken, locationB.Id, "Group B");

        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/groups", caregiverToken, null));
        var groups = (await response.Content.ReadFromJsonAsync<List<GroupResponse>>())!;

        Assert.Contains(groups, g => g.Id == groupA.Id);
        Assert.DoesNotContain(groups, g => g.Id == groupB.Id);
    }

    // ── T034: GET /api/children?groupId= scoped; out-of-scope group → empty array ──

    [Fact]
    public async Task GetChildren_ByGroupId_AsCaregiver_ScopedToEligibleLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Scoping Children Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, locationA.Id, "Group A");
        var groupB = await CreateGroupAsync(client, org.AccessToken, locationB.Id, "Group B");
        var childA = await CreateChildAsync(client, org.AccessToken);
        var childB = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, groupA.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, groupB.Id);

        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id);

        var eligibleResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children?groupId={groupA.Id}", caregiverToken));
        var eligibleChildren = (await eligibleResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.Contains(eligibleChildren, c => c.Id == childA.Id);

        var outOfScopeResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children?groupId={groupB.Id}", caregiverToken));
        Assert.Equal(HttpStatusCode.OK, outOfScopeResponse.StatusCode);
        var outOfScopeChildren = (await outOfScopeResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.Empty(outOfScopeChildren);
    }

    // ── T034 (continued): GET /api/children/{id} direct lookup also scoped ──────

    [Fact]
    public async Task GetChildById_AsCaregiver_OutOfScopeChild_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Scoping ChildById Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupB = await CreateGroupAsync(client, org.AccessToken, locationB.Id, "Group B");
        var childB = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, groupB.Id);

        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childB.Id}", caregiverToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.child.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T035: director's view remains fully unfiltered ──────────────────────────

    [Fact]
    public async Task GetGroupsAndChildren_AsDirector_RemainUnfiltered()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Scoping Director Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, locationA.Id, "Group A");
        var groupB = await CreateGroupAsync(client, org.AccessToken, locationB.Id, "Group B");

        var groupsResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/groups", org.AccessToken));
        var groups = (await groupsResponse.Content.ReadFromJsonAsync<List<GroupResponse>>())!;
        Assert.Contains(groups, g => g.Id == groupA.Id);
        Assert.Contains(groups, g => g.Id == groupB.Id);

        var childA = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, groupA.Id);
        var childrenResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", org.AccessToken));
        var children = (await childrenResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.Contains(children, c => c.Id == childA.Id);
    }

    // ── T035a: zero eligible locations → empty arrays, never an error ───────────

    [Fact]
    public async Task GetGroupsAndStaffMe_AsCaregiverWithZeroEligibleLocations_ReturnsEmptyNotError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Zero Eligibility Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        await CreateGroupAsync(client, org.AccessToken, location.Id, "Group A");

        // No location eligibility assigned at all.
        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var meResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff/me", caregiverToken));
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = (await meResponse.Content.ReadFromJsonAsync<StaffMeResponse>())!;
        Assert.Empty(me.EligibleLocationIds);

        var groupsResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/groups", caregiverToken));
        Assert.Equal(HttpStatusCode.OK, groupsResponse.StatusCode);
        var groups = (await groupsResponse.Content.ReadFromJsonAsync<List<GroupResponse>>())!;
        Assert.Empty(groups);
    }

    // ── T040b: two caregivers, different locations, never see each other's data ─

    [Fact]
    public async Task TwoCaregivers_DifferentLocations_NeverSeeEachOthersData()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Two Caregivers Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, locationA.Id, "Group A");
        var groupB = await CreateGroupAsync(client, org.AccessToken, locationB.Id, "Group B");
        var childA = await CreateChildAsync(client, org.AccessToken);
        var childB = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, groupA.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, groupB.Id);

        var (_, caregiverAToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id);
        var (_, caregiverBToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationB.Id);

        var groupsAsA = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/groups", caregiverAToken)))
            .Content.ReadFromJsonAsync<List<GroupResponse>>())!;
        var groupsAsB = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/groups", caregiverBToken)))
            .Content.ReadFromJsonAsync<List<GroupResponse>>())!;

        Assert.Contains(groupsAsA, g => g.Id == groupA.Id);
        Assert.DoesNotContain(groupsAsA, g => g.Id == groupB.Id);
        Assert.Contains(groupsAsB, g => g.Id == groupB.Id);
        Assert.DoesNotContain(groupsAsB, g => g.Id == groupA.Id);

        var childBAsA = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childB.Id}", caregiverAToken));
        Assert.Equal(HttpStatusCode.NotFound, childBAsA.StatusCode);
        var childAAsB = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childA.Id}", caregiverBToken));
        Assert.Equal(HttpStatusCode.NotFound, childAAsB.StatusCode);
    }
}
