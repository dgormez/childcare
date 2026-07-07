using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 3a (FR-009a): a director ends an active contract entirely (family leaves) with
/// no successor. Also covers FR-009's immutability guarantee for an `ended` contract.
/// </summary>
public class ContractTerminationTests(OrganisationOnboardingWebAppFactory factory)
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
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static List<ContractedDayRequest> Days(params DayOfWeek[] weekdays) =>
        weekdays.Select(w => new ContractedDayRequest(w, new TimeOnly(8, 0), new TimeOnly(17, 0))).ToList();

    private static async Task<ContractResponse> CreateDraftAsync(HttpClient client, string accessToken, Guid childId, Guid locationId, params DayOfWeek[] weekdays)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken,
            new CreateContractRequest(locationId, new DateOnly(2026, 1, 1), null, Days(weekdays), 3500, null)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    private static async Task<ContractResponse> CreateAndActivateAsync(HttpClient client, string accessToken, Guid childId, Guid locationId, params DayOfWeek[] weekdays)
    {
        var contract = await CreateDraftAsync(client, accessToken, childId, locationId, weekdays);
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    // ── T049: terminate an active contract ───────────────────────────────────────

    [Fact]
    public async Task TerminateContract_OnActive_EndsWithNoSuccessor()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Terminate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);

        var terminateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        var terminated = (await terminateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("ended", terminated.Status);
        Assert.Equal(new DateOnly(2026, 6, 30), terminated.EndDate);

        var historyResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/contracts", org.AccessToken));
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ContractResponse>>())!;
        Assert.Single(history);
    }

    // ── T050: terminated contract's weekdays no longer block ────────────────────

    [Fact]
    public async Task AfterTermination_NewContractReusingSameWeekdays_ActivatesSuccessfully()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Terminate Reuse Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));

        var newContract = await CreateDraftAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{newContract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
    }

    // ── T051: termination date before contract start date ────────────────────────

    [Fact]
    public async Task TerminateContract_EndDateBeforeStartDate_ReturnsInvalid()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Terminate Invalid Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);

        var terminateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2025, 12, 31))));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, terminateResponse.StatusCode);
        Assert.Contains("errors.contract.termination_date_invalid", await terminateResponse.Content.ReadAsStringAsync());
    }

    // ── T052: terminate a draft or already-ended contract ────────────────────────

    [Fact]
    public async Task TerminateContract_DraftOrAlreadyEnded_ReturnsNotActive()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Terminate NotActive Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var draft = await CreateDraftAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        var terminateDraftResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{draft.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));
        Assert.Equal(HttpStatusCode.Conflict, terminateDraftResponse.StatusCode);
        Assert.Contains("errors.contract.not_active", await terminateDraftResponse.Content.ReadAsStringAsync());

        var active = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Tuesday);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{active.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));
        var terminateAgainResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{active.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 7, 30))));
        Assert.Equal(HttpStatusCode.Conflict, terminateAgainResponse.StatusCode);
        Assert.Contains("errors.contract.not_active", await terminateAgainResponse.Content.ReadAsStringAsync());
    }

    // ── T052a: an ended contract's terms cannot be edited via PUT (FR-009) ───────

    [Fact]
    public async Task UpdateContract_OnTerminatedContract_ReturnsNotDraft()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Terminate Immutable Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2026, 6, 30))));

        var updateRequest = new UpdateContractRequest(new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Monday), 5000, null);
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/contracts/{contract.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        Assert.Contains("errors.contract.not_draft", await updateResponse.Content.ReadAsStringAsync());
    }
}
