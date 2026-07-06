using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 3 (SC-005): a location can be deactivated (soft-delete, excluded from active
/// listings, never hard-deleted, FR-008/FR-009) and reactivated (clarified session 2026-07-06),
/// including down to zero active locations (FR-016) and idempotently.
/// </summary>
public class LocationDeactivationTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name = "Building")
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 15)));
        return (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
    }

    // ── T028: deactivate excludes from default list, includeDeactivated shows it ─

    [Fact]
    public async Task Deactivate_ExcludesFromDefaultList_ButVisibleWithIncludeDeactivated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Deactivation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = (await deactivateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.NotNull(deactivated.DeactivatedAt);

        var defaultListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations", org.AccessToken));
        var defaultList = (await defaultListResponse.Content.ReadFromJsonAsync<List<LocationResponse>>())!;
        Assert.DoesNotContain(defaultList, l => l.Id == location.Id);

        var includeDeactivatedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations?includeDeactivated=true", org.AccessToken));
        var includeDeactivatedList = (await includeDeactivatedResponse.Content.ReadFromJsonAsync<List<LocationResponse>>())!;
        Assert.Contains(includeDeactivatedList, l => l.Id == location.Id);
    }

    // ── T029: reactivate restores it, all prior settings intact ─────────────────

    [Fact]
    public async Task Reactivate_RestoresLocation_WithPriorSettingsIntact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reactivation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Original Name");

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));

        var reactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        var reactivated = (await reactivateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Null(reactivated.DeactivatedAt);
        Assert.Equal("Original Name", reactivated.Name);

        var defaultListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations", org.AccessToken));
        var defaultList = (await defaultListResponse.Content.ReadFromJsonAsync<List<LocationResponse>>())!;
        Assert.Contains(defaultList, l => l.Id == location.Id);
    }

    // ── T030: deactivating every location down to zero active is permitted ─────

    [Fact]
    public async Task DeactivateAllLocations_ZeroActiveLocationsPermitted()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Zero Active Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location1 = await CreateLocationAsync(client, org.AccessToken, "Loc 1");
        var location2 = await CreateLocationAsync(client, org.AccessToken, "Loc 2");

        var deactivate1 = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location1.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivate1.StatusCode);
        var deactivate2 = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location2.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivate2.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<List<LocationResponse>>())!;
        Assert.Empty(list);
    }

    // ── T031: deactivate/reactivate are idempotent ───────────────────────────────

    [Fact]
    public async Task DeactivateAndReactivate_AreIdempotent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Idempotent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);

        var firstDeactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        var secondDeactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstDeactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondDeactivate.StatusCode);

        var firstReactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/reactivate", org.AccessToken));
        var secondReactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstReactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondReactivate.StatusCode);

        var final = (await secondReactivate.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Null(final.DeactivatedAt);
    }
}
