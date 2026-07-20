using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Feature 022, User Story 5: recording a child's encrypted National Register Number. See
/// VerifyChildIdentityTests for the identity-verification tests this feature also adds.
/// </summary>
public class SetChildNrnTests(OrganisationOnboardingWebAppFactory factory)
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

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
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
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    // ── T059: SetChildNrnCommand happy path (both plain and dotted/dashed input) ─

    [Theory]
    [InlineData("85073003371")]
    [InlineData("85.07.30-033.71")]
    public async Task SetChildNrn_ValidFormat_PersistsEncryptedAndLast4(string rawNrn)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Set Nrn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/children/{child.Id}/nrn", org.AccessToken,
            new SetChildNrnRequest(rawNrn)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Equal("3371", updated.NrnLast4);

        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var db = resolver.ForSchema(schema);
        var stored = await db.Children.SingleAsync(c => c.Id == child.Id);
        Assert.NotNull(stored.EncryptedNrn);
        Assert.DoesNotContain("85073003371", stored.EncryptedNrn);
    }

    // ── T060: invalid NRN format → 422, persists nothing ─────────────────────────

    [Fact]
    public async Task SetChildNrn_InvalidFormat_Returns422AndPersistsNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Set Nrn Invalid Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/children/{child.Id}/nrn", org.AccessToken,
            new SetChildNrnRequest("12345")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child.nrn_invalid_format", await response.Content.ReadAsStringAsync());

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(reloaded.NrnLast4);
    }

    // ── T061: raw NRN never appears in the response ──────────────────────────────

    [Fact]
    public async Task SetChildNrn_ResponseNeverContainsRawNrn()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Set Nrn NoLeak Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/children/{child.Id}/nrn", org.AccessToken,
            new SetChildNrnRequest("85073003371")));

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("85073003371", raw);
    }

    // ── T061a: 404 for a non-existent child on the NRN endpoint ──────────────────

    [Fact]
    public async Task SetChildNrn_NonExistentChild_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Set Nrn NotFound Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/children/{Guid.NewGuid()}/nrn", org.AccessToken,
            new SetChildNrnRequest("85073003371")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.child.not_found", await response.Content.ReadAsStringAsync());
    }
}
