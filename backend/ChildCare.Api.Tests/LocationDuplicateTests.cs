using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 4 (SC-006): duplicating a location clones its fields into a new, fully
/// independent record with no persisted link back to the source (FR-015, research.md R5).
/// </summary>
public class LocationDuplicateTests(OrganisationOnboardingWebAppFactory factory)
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

    // ── T036: duplicate copies all fields, no reference back to source ──────────

    [Fact]
    public async Task Duplicate_CopiesAllFields_NoReferenceToSource()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Duplicate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createRequest = new CreateLocationRequest("Source Building", "Source Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 25);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, createRequest));
        var source = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var updateRequest = new UpdateLocationRequest(
            source.Name, source.Address, source.Phone, source.Email, source.MaxCapacity,
            NaamLocatie: "Officiele Naam", Dossiernummer: "999999", Verantwoordelijke: "Responsible Person",
            FlexPermission: true, BoPermission: true);
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{source.Id}", org.AccessToken, updateRequest));
        source = (await updateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var duplicateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{source.Id}/duplicate", org.AccessToken));
        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);

        var duplicate = (await duplicateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.NotEqual(source.Id, duplicate.Id);
        Assert.Equal(source.Name, duplicate.Name);
        Assert.Equal(source.Address, duplicate.Address);
        Assert.Equal(source.Phone, duplicate.Phone);
        Assert.Equal(source.Email, duplicate.Email);
        Assert.Equal(source.MaxCapacity, duplicate.MaxCapacity);
        Assert.Equal(source.NaamLocatie, duplicate.NaamLocatie);
        Assert.Equal(source.Dossiernummer, duplicate.Dossiernummer);
        Assert.Equal(source.Verantwoordelijke, duplicate.Verantwoordelijke);
        Assert.Equal(source.FlexPermission, duplicate.FlexPermission);
        Assert.Equal(source.BoPermission, duplicate.BoPermission);

        // No field anywhere in the response references the source location's id.
        var rawJson = await duplicateResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                Assert.NotEqual(source.Id.ToString(), prop.Value.GetString());
        }
    }

    // ── T037: editing the duplicate does not affect the source ──────────────────

    [Fact]
    public async Task EditingDuplicate_DoesNotAffectSource()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Duplicate Edit Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", org.AccessToken,
            new CreateLocationRequest("Source", "Source Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 10)));
        var source = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var duplicateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{source.Id}/duplicate", org.AccessToken));
        var duplicate = (await duplicateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var updateRequest = new UpdateLocationRequest(
            "Renamed Duplicate", "New Address", duplicate.Phone, duplicate.Email, duplicate.MaxCapacity,
            duplicate.NaamLocatie, duplicate.Dossiernummer, duplicate.Verantwoordelijke, duplicate.FlexPermission, duplicate.BoPermission);
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{duplicate.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var sourceReloadedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{source.Id}", org.AccessToken));
        var sourceReloaded = (await sourceReloadedResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("Source", sourceReloaded.Name);
        Assert.Equal("Source Address", sourceReloaded.Address);
    }
}
