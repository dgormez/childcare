using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 2 (FR-005/FR-006, constitution Principle II): a child may hold two simultaneous
/// active contracts at different locations provided their contracted weekdays never overlap,
/// enforced atomically even under concurrent activation attempts. No new implementation here —
/// ActivateContractCommand + ContractActivationChecker (built in US1/Foundational) already
/// implement the cross-location day-overlap check and the per-child advisory lock; this file
/// proves that shared mechanism holds at the scope the constitution specifically names.
/// </summary>
public class ContractSplitLocationTests(OrganisationOnboardingWebAppFactory factory)
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
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static List<ContractedDayRequest> Days(params DayOfWeek[] weekdays) =>
        weekdays.Select(w => new ContractedDayRequest(w, new TimeOnly(8, 0), new TimeOnly(17, 0))).ToList();

    private static async Task<ContractResponse> CreateDraftAsync(HttpClient client, string accessToken, Guid childId, Guid locationId, params DayOfWeek[] weekdays)
    {
        var request = new CreateContractRequest(locationId, new DateOnly(2026, 1, 1), null, Days(weekdays), 3500, null);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    private static Task<HttpResponseMessage> ActivateAsync(HttpClient client, string accessToken, Guid contractId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contractId}/activate", accessToken));

    // ── T038: two locations, non-overlapping weekdays, both succeed ─────────────

    [Fact]
    public async Task ActivateContracts_AtTwoLocations_NonOverlappingDays_BothSucceed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Split Location Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var child = await CreateChildAsync(client, org.AccessToken);

        var contractA = await CreateDraftAsync(client, org.AccessToken, child.Id, locationA.Id, DayOfWeek.Monday, DayOfWeek.Tuesday);
        var contractB = await CreateDraftAsync(client, org.AccessToken, child.Id, locationB.Id, DayOfWeek.Wednesday, DayOfWeek.Thursday);

        var activateAResponse = await ActivateAsync(client, org.AccessToken, contractA.Id);
        var activateBResponse = await ActivateAsync(client, org.AccessToken, contractB.Id);

        Assert.Equal(HttpStatusCode.OK, activateAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activateBResponse.StatusCode);
    }

    // ── T039: third location with an overlapping weekday is rejected ────────────

    [Fact]
    public async Task ActivateContract_OverlappingWeekdayAtDifferentLocation_ReturnsDayOverlap()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Split Overlap Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationC = await CreateLocationAsync(client, org.AccessToken, "Location C");
        var child = await CreateChildAsync(client, org.AccessToken);

        var contractA = await CreateDraftAsync(client, org.AccessToken, child.Id, locationA.Id, DayOfWeek.Monday, DayOfWeek.Tuesday);
        Assert.Equal(HttpStatusCode.OK, (await ActivateAsync(client, org.AccessToken, contractA.Id)).StatusCode);

        var contractC = await CreateDraftAsync(client, org.AccessToken, child.Id, locationC.Id, DayOfWeek.Tuesday, DayOfWeek.Wednesday);
        var activateCResponse = await ActivateAsync(client, org.AccessToken, contractC.Id);

        Assert.Equal(HttpStatusCode.Conflict, activateCResponse.StatusCode);
        Assert.Contains("errors.contract.day_overlap", await activateCResponse.Content.ReadAsStringAsync());

        var getAResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contractA.Id}", org.AccessToken));
        var reloadedA = (await getAResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("active", reloadedA.Status);
    }

    // ── T040: concurrent conflicting activations — exactly one succeeds ─────────

    [Fact]
    public async Task ActivateContracts_ConcurrentlyConflicting_ExactlyOneSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Split Concurrency Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var child = await CreateChildAsync(client, org.AccessToken);

        var contractA = await CreateDraftAsync(client, org.AccessToken, child.Id, locationA.Id, DayOfWeek.Monday);
        var contractB = await CreateDraftAsync(client, org.AccessToken, child.Id, locationB.Id, DayOfWeek.Monday);

        var results = await Task.WhenAll(
            ActivateAsync(client, org.AccessToken, contractA.Id),
            ActivateAsync(client, org.AccessToken, contractB.Id));

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);
    }

    // ── T041: same-day transition (independent create+activate) not blocked ─────

    [Fact]
    public async Task ActivateContract_SameDayTransitionAtSameLocation_NotBlockedByEndedPredecessor()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Split Transition Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var child = await CreateChildAsync(client, org.AccessToken);

        var firstContract = await CreateDraftAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        Assert.Equal(HttpStatusCode.OK, (await ActivateAsync(client, org.AccessToken, firstContract.Id)).StatusCode);

        var terminateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{firstContract.Id}/terminate", org.AccessToken,
            new TerminateContractRequest(new DateOnly(2026, 6, 30))));
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        var secondContract = await CreateDraftAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        var activateSecondResponse = await ActivateAsync(client, org.AccessToken, secondContract.Id);
        Assert.Equal(HttpStatusCode.OK, activateSecondResponse.StatusCode);
    }
}
