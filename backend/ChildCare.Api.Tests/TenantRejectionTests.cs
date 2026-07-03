using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ChildCare.Api.Middleware;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 2 (SC-002): every FR-006/007/008/008a rejection path denies a non-exempt request
/// before any organisation data is touched. Tokens are crafted directly (not obtained through
/// the normal register/login flow) so each test can control the exact tenant_id claim shape
/// under test (quickstart.md Scenario 2).
/// </summary>
public class TenantRejectionTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static string BuildToken(Guid userId, string? tenantIdClaim)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, $"{userId:N}@example.com"),
        };
        if (tenantIdClaim is not null)
            claims.Add(new Claim("tenant_id", tenantIdClaim));

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebAppFactoryBase.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer:             TestWebAppFactoryBase.TestJwtIssuer,
            audience:           TestWebAppFactoryBase.TestJwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpRequestMessage ResendVerificationRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/resend-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<string> GetErrorKeyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["errorKey"].ToString()!;
    }

    // ── T027: missing tenant_id claim ───────────────────────────────────────────

    [Fact]
    public async Task NonExemptRoute_JwtWithNoTenantIdClaim_RejectedWithErrorsTenantMissing()
    {
        var token = BuildToken(Guid.NewGuid(), tenantIdClaim: null);

        var response = await factory.CreateClient().SendAsync(ResendVerificationRequest(token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("errors.tenant.missing", await GetErrorKeyAsync(response));
    }

    // ── Regression: a route that doesn't exist must 404, not be swallowed into a tenant
    // rejection — found via quickstart.md Scenario 5 manual verification (tasks.md T037): the
    // now-deleted /api/habits was returning 401 errors.tenant.missing instead of 404, because
    // TenantMiddleware ran before an unmatched route could fall through to the framework's
    // ordinary 404 handling.

    [Fact]
    public async Task UnmatchedRoute_ReturnsNotFound_NotATenantRejection()
    {
        var response = await factory.CreateClient().GetAsync("/api/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── T028: unknown tenant_id ──────────────────────────────────────────────────

    [Fact]
    public async Task NonExemptRoute_JwtWithUnknownTenantId_RejectedWithErrorsTenantNotFound()
    {
        var token = BuildToken(Guid.NewGuid(), tenantIdClaim: Guid.NewGuid().ToString());

        var response = await factory.CreateClient().SendAsync(ResendVerificationRequest(token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("errors.tenant.not_found", await GetErrorKeyAsync(response));
    }

    // ── T029: not-yet-ready tenant ───────────────────────────────────────────────

    [Fact]
    public async Task NonExemptRoute_JwtForNotReadyTenant_RejectedWithErrorsTenantNotReady()
    {
        Guid tenantId;
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
            var tenant = new Tenant
            {
                Name               = "Not Ready Org",
                Slug               = $"not-ready-{Guid.NewGuid():N}",
                SchemaName         = $"tenant_not_ready_{Guid.NewGuid():N}",
                ProvisioningStatus = ProvisioningStatus.Provisioning,
                CreatedFromInvitationId = Guid.NewGuid(),
            };
            publicDb.Tenants.Add(tenant);
            await publicDb.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        var token = BuildToken(Guid.NewGuid(), tenantIdClaim: tenantId.ToString());

        var response = await factory.CreateClient().SendAsync(ResendVerificationRequest(token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("errors.tenant.not_ready", await GetErrorKeyAsync(response));
    }

    // ── T030: lookup failure is indistinguishable from unknown, but logged server-side ─

    [Fact]
    public async Task NonExemptRoute_WhenTenantLookupThrows_RejectedIdenticallyToUnknownAndLogsServerSide()
    {
        var client = factory.CreateClient();

        var invite = await CreateInvitationAsync(client, "lookup-failure-director@example.com");
        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invite.Token, "Lookup Failure Org", "Lookup Failure Director", invite.Email, "password123"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = (await registerResponse.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;

        var middleware = factory.Services.GetRequiredService<TenantMiddleware>();
        middleware.FailureInjectionHookForTests = () => throw new InvalidOperationException("simulated tenant lookup failure");

        HttpResponseMessage failureResponse;
        try
        {
            failureResponse = await client.SendAsync(ResendVerificationRequest(registered.AccessToken));
        }
        finally
        {
            middleware.FailureInjectionHookForTests = null; // clear before any other test runs
        }

        // FR-008a: byte-for-byte the same outcome as the unknown-tenant case (T028) — a valid,
        // known, Ready tenant whose lookup happened to throw must not be distinguishable from
        // one that simply doesn't exist.
        Assert.Equal(HttpStatusCode.Forbidden, failureResponse.StatusCode);
        Assert.Equal("errors.tenant.not_found", await GetErrorKeyAsync(failureResponse));

        Assert.Contains(factory.LogCapture.Entries, e =>
            e.Level == LogLevel.Error &&
            e.Exception is InvalidOperationException { Message: "simulated tenant lookup failure" });
    }

    // ── T031: malformed (non-GUID) tenant_id claim ──────────────────────────────

    [Fact]
    public async Task NonExemptRoute_JwtWithMalformedTenantId_RejectedSameAsUnknown()
    {
        // spec.md Edge Cases: a garbled/malformed claim is treated the same as "unknown
        // organisation" (FR-007), not the same as a missing claim (FR-006).
        var token = BuildToken(Guid.NewGuid(), tenantIdClaim: "not-a-guid");

        var response = await factory.CreateClient().SendAsync(ResendVerificationRequest(token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("errors.tenant.not_found", await GetErrorKeyAsync(response));
    }

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
}
