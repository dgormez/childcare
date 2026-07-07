using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 4: recording vaccine entries and the computed due-alert flag
/// (FR-010/FR-011), including the CHK002 future-date fix.</summary>
public class ChildVaccinationTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    // ── T071: future next-due-date → not due ─────────────────────────────────────

    [Fact]
    public async Task RecordVaccination_FutureNextDueDate_NotFlaggedDue()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Future Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var request = new RecordVaccinationRequest("DTP", new DateOnly(2026, 1, 1), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)));
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccinations", org.AccessToken, request));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccinations", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccinationResponse>>())!;
        Assert.False(list.Single().IsDue);
    }

    // ── T072: past next-due-date → due ────────────────────────────────────────────

    [Fact]
    public async Task RecordVaccination_PastNextDueDate_FlaggedDue()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Due Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var request = new RecordVaccinationRequest("MMR", new DateOnly(2025, 1, 1), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccinations", org.AccessToken, request));

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccinations", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccinationResponse>>())!;
        Assert.True(list.Single().IsDue);
    }

    // ── T073: no next-due-date → never flagged due ───────────────────────────────

    [Fact]
    public async Task RecordVaccination_NoNextDueDate_NeverFlaggedDue()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine OneTime Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var request = new RecordVaccinationRequest("HepB", new DateOnly(2023, 6, 1), null);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccinations", org.AccessToken, request));

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccinations", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccinationResponse>>())!;
        Assert.False(list.Single().IsDue);
    }

    // ── T089/CHK002: administered date in the future → 422 ──────────────────────

    [Fact]
    public async Task RecordVaccination_FutureDateAdministered_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine FutureAdmin Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var request = new RecordVaccinationRequest("DTP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccinations", org.AccessToken, request));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.vaccination.date_administered_in_future", await response.Content.ReadAsStringAsync());
    }
}
