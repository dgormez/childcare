using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 2 (SC-002): Opgroeien reporting settings (naamLocatie, dossiernummer,
/// verantwoordelijke, flexPermission, boPermission) never block location creation and can be
/// filled in later via update (FR-003, FR-004, FR-005).
/// </summary>
public class LocationOpgroeienSettingsTests(OrganisationOnboardingWebAppFactory factory)
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

    // ── T025: creation without Opgroeien fields succeeds; later edit adds them ──

    [Fact]
    public async Task CreateWithoutOpgroeienFields_Succeeds_LaterUpdateAddsThem()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Opgroeien Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createRequest = new CreateLocationRequest("Building", "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 15);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, createRequest));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Null(created.NaamLocatie);
        Assert.Null(created.Dossiernummer);
        Assert.Null(created.Verantwoordelijke);

        var updateRequest = new UpdateLocationRequest(
            created.Name, created.Address, created.Phone, created.Email, created.MaxCapacity,
            NaamLocatie: "Officieel Geregistreerde Naam",
            Dossiernummer: "123456",
            Verantwoordelijke: "Jane Director",
            FlexPermission: created.FlexPermission,
            BoPermission: created.BoPermission);

        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{created.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{created.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("Officieel Geregistreerde Naam", reloaded.NaamLocatie);
        Assert.Equal("123456", reloaded.Dossiernummer);
        Assert.Equal("Jane Director", reloaded.Verantwoordelijke);
    }

    // ── T026: flexPermission/boPermission default false, toggle to true persists ─

    [Fact]
    public async Task FlexAndBoPermission_DefaultFalse_ToggleToTruePersists()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Permission Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createRequest = new CreateLocationRequest("Building", "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 15);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, createRequest));
        var created = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.False(created.FlexPermission);
        Assert.False(created.BoPermission);

        var updateRequest = new UpdateLocationRequest(
            created.Name, created.Address, created.Phone, created.Email, created.MaxCapacity,
            created.NaamLocatie, created.Dossiernummer, created.Verantwoordelijke,
            FlexPermission: true, BoPermission: true);

        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{created.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{created.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.True(reloaded.FlexPermission);
        Assert.True(reloaded.BoPermission);
    }
}
