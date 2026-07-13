using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 5 (SC-005): a child can be deactivated (soft-delete, excluded from
/// active listings, never hard-deleted, full history preserved, FR-012) and reactivated
/// (FR-014).</summary>
public class ChildDeactivationTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken, string firstName = "Emma") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, "Peanuts", "Severe", null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    // ── T078: deactivate excludes from default list, includeDeactivated shows it ─

    [Fact]
    public async Task Deactivate_ExcludesFromDefaultList_ButVisibleWithIncludeDeactivated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Deactivation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = (await deactivateResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(deactivated.DeactivatedAt);

        var defaultListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", org.AccessToken));
        var defaultList = (await defaultListResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.DoesNotContain(defaultList, c => c.Id == child.Id);

        var includeDeactivatedResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children?includeDeactivated=true", org.AccessToken));
        var includeDeactivatedList = (await includeDeactivatedResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.Contains(includeDeactivatedList, c => c.Id == child.Id);
    }

    // ── T079: full history (medical/contacts/groups/vaccines) survives deactivation ─

    [Fact]
    public async Task Deactivate_PreservesFullMedicalHistory()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child History Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Equal("Peanuts", reloaded.AllergiesDescription);
        Assert.Equal("Severe", reloaded.AllergySeverity);
    }

    // ── T080: reactivate restores active listing ─────────────────────────────────

    [Fact]
    public async Task Reactivate_RestoresChildToActiveListing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Reactivation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));

        var reactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        var reactivated = (await reactivateResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(reactivated.DeactivatedAt);

        var defaultListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", org.AccessToken));
        var defaultList = (await defaultListResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.Contains(defaultList, c => c.Id == child.Id);
    }

    // ── T081: deactivate/reactivate are idempotent ───────────────────────────────

    [Fact]
    public async Task DeactivateAndReactivate_AreIdempotent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Idempotent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var firstDeactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        var secondDeactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstDeactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondDeactivate.StatusCode);

        var firstReactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/reactivate", org.AccessToken));
        var secondReactivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/reactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstReactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondReactivate.StatusCode);

        var final = (await secondReactivate.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(final.DeactivatedAt);
    }
}
