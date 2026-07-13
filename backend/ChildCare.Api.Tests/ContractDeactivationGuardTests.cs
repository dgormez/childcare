using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Proves the ILocationDeactivationGuard/IChildDeactivationGuard implementations built in this
/// feature (research.md R3, fulfilling the extension points features 004/006 reserved) block
/// deactivation while an active contract exists, and stop blocking once it ends. No new
/// implementation — this file is test-only.
/// </summary>
public class ContractDeactivationGuardTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest("Main Building", "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static async Task<ContractResponse> CreateAndActivateAsync(HttpClient client, string accessToken, Guid childId, Guid locationId)
    {
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken,
            new CreateContractRequest(locationId, new DateOnly(2026, 1, 1), null,
                [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null)));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    // ── T061: deactivating a location with an active contract is blocked ────────

    [Fact]
    public async Task DeactivateLocation_WithActiveContract_ReturnsHasActiveDependents()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Guard Location Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, deactivateResponse.StatusCode);
        Assert.Contains("errors.location.has_active_dependents", await deactivateResponse.Content.ReadAsStringAsync());
    }

    // ── T062: deactivating a child with an active contract is blocked ───────────

    [Fact]
    public async Task DeactivateChild_WithActiveContract_ReturnsHasActiveDependents()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Guard Child Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, deactivateResponse.StatusCode);
        Assert.Contains("errors.child.has_active_dependents", await deactivateResponse.Content.ReadAsStringAsync());
    }

    // ── T063: terminating the contract unblocks both deactivations ──────────────

    [Fact]
    public async Task AfterTerminatingContract_BothDeactivationsSucceed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Guard Terminate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id);

        var terminateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        var deactivateLocationResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateLocationResponse.StatusCode);

        var deactivateChildResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateChildResponse.StatusCode);
    }
}
