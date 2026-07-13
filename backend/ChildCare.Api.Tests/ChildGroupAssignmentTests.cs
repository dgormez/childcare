using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 3: a director creates groups and assigns children to them over time,
/// with a full non-overwritten history (FR-008/FR-008a), including the active-location
/// (CHK003) and chronological-order (CHK004) fixes.</summary>
public class ChildGroupAssignmentTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name = "Main Building") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient client, string accessToken, Guid locationId, string name = "Baby Room") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/groups", accessToken, new CreateGroupRequest(name, locationId))))
            .Content.ReadFromJsonAsync<GroupResponse>())!;

    // ── T060: create group, assign child ─────────────────────────────────────────

    [Fact]
    public async Task CreateGroup_AssignChild_AppearsInHistory()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Group Assign Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var group = await CreateGroupAsync(client, org.AccessToken, location.Id);

        var assignResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(group.Id, new DateOnly(2026, 1, 1))));
        Assert.Equal(HttpStatusCode.Created, assignResponse.StatusCode);

        var historyResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/groups", org.AccessToken));
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ChildGroupAssignmentResponse>>())!;
        Assert.Single(history);
        Assert.Null(history[0].EndDate);
    }

    // ── T061: reassign ends the prior assignment automatically ──────────────────

    [Fact]
    public async Task ReassignToNewGroup_EndsPriorAssignment_BothInHistory()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reassign Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var babyRoom = await CreateGroupAsync(client, org.AccessToken, location.Id, "Baby Room");
        var toddlerRoom = await CreateGroupAsync(client, org.AccessToken, location.Id, "Toddler Room");

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(babyRoom.Id, new DateOnly(2026, 1, 1))));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(toddlerRoom.Id, new DateOnly(2026, 7, 1))));

        var historyResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/groups", org.AccessToken));
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ChildGroupAssignmentResponse>>())!;
        Assert.Equal(2, history.Count);

        var babyRoomAssignment = history.Single(a => a.GroupId == babyRoom.Id);
        Assert.Equal(new DateOnly(2026, 6, 30), babyRoomAssignment.EndDate);

        var toddlerRoomAssignment = history.Single(a => a.GroupId == toddlerRoom.Id);
        Assert.Null(toddlerRoomAssignment.EndDate);
    }

    // ── T062: zero group assignments → empty array, no error ────────────────────

    [Fact]
    public async Task Child_WithZeroGroupAssignments_ReturnsEmptyArray()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Zero Groups Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/groups", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = (await response.Content.ReadFromJsonAsync<List<ChildGroupAssignmentResponse>>())!;
        Assert.Empty(history);
    }

    // ── T063: group against a cross-tenant location → 404 ────────────────────────

    [Fact]
    public async Task CreateGroup_LocationFromDifferentOrganisation_Returns404()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Group CrossTenant Org A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"Group CrossTenant Org B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var locationB = await CreateLocationAsync(client, orgB.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/groups", orgA.AccessToken, new CreateGroupRequest("Group", locationB.Id)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.location.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T090/CHK003: group against a deactivated location → 404 ─────────────────

    [Fact]
    public async Task CreateGroup_AgainstDeactivatedLocation_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Group Deactivated Location Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/groups", org.AccessToken, new CreateGroupRequest("Group", location.Id)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.location.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T091/CHK004: out-of-chronological-order assignment → 422 ─────────────────

    [Fact]
    public async Task AssignChildToGroup_StartDateBeforeCurrentlyOpenAssignment_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Chronological Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var babyRoom = await CreateGroupAsync(client, org.AccessToken, location.Id, "Baby Room");
        var toddlerRoom = await CreateGroupAsync(client, org.AccessToken, location.Id, "Toddler Room");

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(babyRoom.Id, new DateOnly(2026, 7, 1))));

        var earlierResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(toddlerRoom.Id, new DateOnly(2026, 1, 1))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlierResponse.StatusCode);
        Assert.Contains("errors.group.out_of_chronological_order", await earlierResponse.Content.ReadAsStringAsync());
    }

    // ── T075/spec.md Edge Cases: eligibility survives the location being deactivated ──

    [Fact]
    public async Task ExistingGroup_SurvivesLocationDeactivation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Group Survives Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var group = await CreateGroupAsync(client, org.AccessToken, location.Id);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/groups?locationId={location.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var groups = (await listResponse.Content.ReadFromJsonAsync<List<GroupResponse>>())!;
        Assert.Contains(groups, g => g.Id == group.Id);
    }
}
