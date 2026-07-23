using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 3 (FR-007/FR-008/FR-009): a director amends an active contract's terms — the
/// original is ended and a successor is created and activated, preserving full history.
/// </summary>
public class ContractAmendmentTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<ContractResponse> CreateAndActivateAsync(HttpClient client, string accessToken, Guid childId, Guid locationId, params DayOfWeek[] weekdays)
    {
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken,
            new CreateContractRequest(locationId, new DateOnly(2026, 1, 1), null, Days(weekdays), 3500, null)));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    // ── T042: amend with a future effective date ─────────────────────────────────

    [Fact]
    public async Task AmendContract_WithFutureEffectiveDate_EndsOriginalAndActivatesSuccessor()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Amend Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var original = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);

        var amendRequest = new AmendContractRequest(
            new DateOnly(2026, 6, 1), location.Id, null, Days(DayOfWeek.Monday, DayOfWeek.Tuesday), 4000, null);
        var amendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{original.Id}/amend", org.AccessToken, amendRequest));
        Assert.Equal(HttpStatusCode.Created, amendResponse.StatusCode);

        var successor = (await amendResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("active", successor.Status);
        Assert.Equal(original.Id, successor.PreviousContractId);
        Assert.Equal(4000, successor.DailyRateCents);

        var originalReloadedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{original.Id}", org.AccessToken));
        var originalReloaded = (await originalReloadedResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("ended", originalReloaded.Status);
        Assert.Equal(new DateOnly(2026, 5, 31), originalReloaded.EndDate);
    }

    // ── T043: history shows both ended original and new active ──────────────────

    [Fact]
    public async Task ListChildContracts_AfterAmendment_ShowsBothMostRecentFirst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Amend History Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var original = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        var amendRequest = new AmendContractRequest(new DateOnly(2026, 6, 1), location.Id, null, Days(DayOfWeek.Monday), 4000, null);
        var amendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{original.Id}/amend", org.AccessToken, amendRequest));
        var successor = (await amendResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var historyResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/contracts", org.AccessToken));
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ContractResponse>>())!;
        Assert.Equal(2, history.Count);
        Assert.Contains(history, c => c.Id == original.Id && c.Status == "ended");
        Assert.Contains(history, c => c.Id == successor.Id && c.Status == "active");
    }

    // ── T044: effective start date not after current start date ─────────────────

    [Fact]
    public async Task AmendContract_EffectiveDateOnOrBeforeCurrentStartDate_ReturnsInvalid()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Amend Invalid Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var original = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);

        var amendRequest = new AmendContractRequest(new DateOnly(2026, 1, 1), location.Id, null, Days(DayOfWeek.Monday), 4000, null);
        var amendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{original.Id}/amend", org.AccessToken, amendRequest));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, amendResponse.StatusCode);
        Assert.Contains("errors.contract.amendment_start_date_invalid", await amendResponse.Content.ReadAsStringAsync());

        var reloadedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{original.Id}", org.AccessToken));
        var reloaded = (await reloadedResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("active", reloaded.Status);
    }

    // ── T045: amendment causing a conflict rolls back entirely ───────────────────

    [Fact]
    public async Task AmendContract_CausingDayOverlap_RollsBackAndLeavesOriginalActive()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Amend Overlap Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var child = await CreateChildAsync(client, org.AccessToken);

        var contractA = await CreateAndActivateAsync(client, org.AccessToken, child.Id, locationA.Id, DayOfWeek.Monday);
        await CreateAndActivateAsync(client, org.AccessToken, child.Id, locationB.Id, DayOfWeek.Wednesday);

        // Amend contractA to also cover Wednesday — conflicts with the still-active Location B contract.
        var amendRequest = new AmendContractRequest(
            new DateOnly(2026, 6, 1), locationA.Id, null, Days(DayOfWeek.Monday, DayOfWeek.Wednesday), 4000, null);
        var amendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contractA.Id}/amend", org.AccessToken, amendRequest));

        Assert.Equal(HttpStatusCode.Conflict, amendResponse.StatusCode);
        Assert.Contains("errors.contract.day_overlap", await amendResponse.Content.ReadAsStringAsync());

        var reloadedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contractA.Id}", org.AccessToken));
        var reloaded = (await reloadedResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("active", reloaded.Status);
        Assert.Null(reloaded.EndDate);

        var historyResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/contracts", org.AccessToken));
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ContractResponse>>())!;
        Assert.Equal(2, history.Count);
    }

    // ── T046: amend a non-active contract ────────────────────────────────────────

    [Fact]
    public async Task AmendContract_NotActive_ReturnsNotActive()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Amend NonActive Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken,
            new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Monday), 3500, null)));
        var draft = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var amendRequest = new AmendContractRequest(new DateOnly(2026, 6, 1), location.Id, null, Days(DayOfWeek.Monday), 4000, null);
        var amendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{draft.Id}/amend", org.AccessToken, amendRequest));

        Assert.Equal(HttpStatusCode.Conflict, amendResponse.StatusCode);
        Assert.Contains("errors.contract.not_active", await amendResponse.Content.ReadAsStringAsync());
    }
}
