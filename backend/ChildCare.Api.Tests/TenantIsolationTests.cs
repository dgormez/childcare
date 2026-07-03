using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 1 (SC-001): a resolved organisation's requests are structurally confined to that
/// organisation's own schema — including under concurrency and connection reuse. Uses
/// POST /api/auth/resend-verification (the only non-exempt, tenant-scoped, side-effecting
/// route the app currently has — feature 001's Admin/Organisation endpoints are exempt) and
/// asserts directly against each organisation's own schema, not just the HTTP response: a
/// wrong-schema lookup would silently no-op (200 OK) rather than error, so only inspecting the
/// database proves the write landed in the right place (quickstart.md Scenario 1).
/// </summary>
public class TenantIsolationTests(OrganisationOnboardingWebAppFactory factory)
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

    private async Task<string?> GetEmailVerificationTokenAsync(string schemaName, Guid userId)
    {
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        return user.EmailVerificationToken;
    }

    private async Task<int> CountUsersAsync(string schemaName)
    {
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        return await db.Users.CountAsync();
    }

    private static HttpRequestMessage ResendVerificationRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/resend-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    // ── T024: single request only touches its own schema ───────────────────────

    [Fact]
    public async Task ResendVerification_ForOneOrganisation_OnlyAffectsThatOrganisationsSchema()
    {
        var client = factory.CreateClient();

        var orgA = await RegisterOrgAsync(client, "Isolation Org A", "director-a-single@example.com");
        var orgB = await RegisterOrgAsync(client, "Isolation Org B", "director-b-single@example.com");

        var schemaA = await GetSchemaNameAsync(orgA.Organisation.Id);
        var schemaB = await GetSchemaNameAsync(orgB.Organisation.Id);

        var originalTokenA = await GetEmailVerificationTokenAsync(schemaA, orgA.Director.Id);
        var originalTokenB = await GetEmailVerificationTokenAsync(schemaB, orgB.Director.Id);

        var response = await client.SendAsync(ResendVerificationRequest(orgA.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newTokenA = await GetEmailVerificationTokenAsync(schemaA, orgA.Director.Id);
        var unchangedTokenB = await GetEmailVerificationTokenAsync(schemaB, orgB.Director.Id);

        Assert.NotEqual(originalTokenA, newTokenA);   // Org A's own schema was written
        Assert.Equal(originalTokenB, unchangedTokenB); // Org B's schema was never touched
    }

    // ── T025: concurrent requests from two organisations never leak tenant context ─

    [Fact]
    public async Task ResendVerification_ConcurrentAcrossTwoOrganisations_NeverLeaksTenantContext()
    {
        var client = factory.CreateClient();

        var orgA = await RegisterOrgAsync(client, "Concurrent Org A", "director-a-concurrent@example.com");
        var orgB = await RegisterOrgAsync(client, "Concurrent Org B", "director-b-concurrent@example.com");

        var schemaA = await GetSchemaNameAsync(orgA.Organisation.Id);
        var schemaB = await GetSchemaNameAsync(orgB.Organisation.Id);

        var originalTokenA = await GetEmailVerificationTokenAsync(schemaA, orgA.Director.Id);
        var originalTokenB = await GetEmailVerificationTokenAsync(schemaB, orgB.Director.Id);

        // Several concurrent rounds — if TenantMiddleware's per-request CurrentTenantService
        // ever leaked across concurrent requests (e.g. if it were mistakenly Singleton), a
        // wrong-schema lookup would silently no-op rather than error, so only the final
        // database state (not the HTTP responses) proves isolation held throughout.
        for (var round = 0; round < 5; round++)
        {
            var responses = await Task.WhenAll(
                client.SendAsync(ResendVerificationRequest(orgA.AccessToken)),
                client.SendAsync(ResendVerificationRequest(orgB.AccessToken)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        }

        var finalTokenA = await GetEmailVerificationTokenAsync(schemaA, orgA.Director.Id);
        var finalTokenB = await GetEmailVerificationTokenAsync(schemaB, orgB.Director.Id);

        Assert.NotEqual(originalTokenA, finalTokenA); // every round's write landed in Org A's own schema
        Assert.NotEqual(originalTokenB, finalTokenB); // every round's write landed in Org B's own schema

        // No cross-schema inserts happened as a side effect of the concurrent traffic.
        Assert.Equal(1, await CountUsersAsync(schemaA));
        Assert.Equal(1, await CountUsersAsync(schemaB));
    }

    // ── T026: sequential requests reusing the same connection never resolve stale ──

    [Fact]
    public async Task ResendVerification_SequentialRequestsOnSameHttpClient_NeverResolveStaleSchema()
    {
        // A single HttpClient (and therefore, plausibly, a reused underlying connection) issues
        // requests for two different organisations back-to-back — the second request's schema
        // must never be stale from the first (spec.md Edge Cases).
        var client = factory.CreateClient();

        var orgA = await RegisterOrgAsync(client, "Sequential Org A", "director-a-sequential@example.com");
        var orgB = await RegisterOrgAsync(client, "Sequential Org B", "director-b-sequential@example.com");

        var schemaA = await GetSchemaNameAsync(orgA.Organisation.Id);
        var schemaB = await GetSchemaNameAsync(orgB.Organisation.Id);

        var originalTokenA = await GetEmailVerificationTokenAsync(schemaA, orgA.Director.Id);
        var originalTokenB = await GetEmailVerificationTokenAsync(schemaB, orgB.Director.Id);

        var responseA = await client.SendAsync(ResendVerificationRequest(orgA.AccessToken));
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);

        var responseB = await client.SendAsync(ResendVerificationRequest(orgB.AccessToken));
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

        var newTokenA = await GetEmailVerificationTokenAsync(schemaA, orgA.Director.Id);
        var newTokenB = await GetEmailVerificationTokenAsync(schemaB, orgB.Director.Id);

        Assert.NotEqual(originalTokenA, newTokenA); // Org A's request resolved to Org A's schema
        Assert.NotEqual(originalTokenB, newTokenB); // Org B's request resolved to Org B's schema, not stale from A
    }
}
