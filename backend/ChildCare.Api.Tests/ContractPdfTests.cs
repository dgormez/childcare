using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 4 (FR-010/FR-011): a director generates a contract PDF, available for
/// any status and localized to nl/fr/en (defaulting to nl).</summary>
public class ContractPdfTests(OrganisationOnboardingWebAppFactory factory)
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
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static async Task<ContractResponse> CreateContractAsync(HttpClient client, string accessToken, Guid childId, Guid locationId)
    {
        var request = new CreateContractRequest(
            locationId, new DateOnly(2026, 1, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))],
            3500, new ContractConsentRequest(true, false, false, false, false));
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    // ── T055: active contract PDF ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateContractPdf_ForActiveContract_ReturnsValidPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Pdf Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateContractAsync(client, org.AccessToken, child.Id, location.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));

        var pdfResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);

        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    // ── T056: draft contract PDF still generated ─────────────────────────────────

    [Fact]
    public async Task GenerateContractPdf_ForDraftContract_ReturnsValidPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Pdf Draft Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateContractAsync(client, org.AccessToken, child.Id, location.Id);

        var pdfResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    // ── T057: nonexistent contract ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateContractPdf_NonexistentContract_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Pdf Missing Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var pdfResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{Guid.NewGuid()}/pdf", org.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, pdfResponse.StatusCode);
        Assert.Contains("errors.contract.not_found", await pdfResponse.Content.ReadAsStringAsync());
    }

    // ── T057a: locale defaulting and selection ───────────────────────────────────

    [Fact]
    public async Task GenerateContractPdf_WithAndWithoutLocale_ReturnsPdfEachTime()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Pdf Locale Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateContractAsync(client, org.AccessToken, child.Id, location.Id);

        var defaultResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);

        var frResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf?locale=fr", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, frResponse.StatusCode);

        var enResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf?locale=en", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, enResponse.StatusCode);

        var unknownLocaleResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf?locale=xx", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unknownLocaleResponse.StatusCode);
    }
}
