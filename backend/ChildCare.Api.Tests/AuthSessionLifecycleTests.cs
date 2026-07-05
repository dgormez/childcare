using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 4 (P4): refresh, logout, account deletion, email verification, and password
/// reset all keep working, now migrated to MediatR and slug-aware (research.md R1/R2),
/// without regressing the per-device session guarantees the pre-feature-003 skeleton already
/// had (quickstart.md Scenario 5 & 6).
/// </summary>
public class AuthSessionLifecycleTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<RegisterOrganisationResponse> RegisterOrgAsync(HttpClient client, string orgName, string email, string password = "password123")
    {
        var invitation = await CreateInvitationAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, orgName, $"{orgName} Director", email, password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;
    }

    private static async Task<AuthSessionResponse> LoginAsync(HttpClient client, string slug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    private static async Task<string> GetErrorKeyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["errorKey"].ToString()!;
    }

    private async Task<string> GetPasswordResetTokenAsync(string schemaName, string email)
    {
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return user.PasswordResetToken!;
    }

    // ── T046: logging out one device doesn't invalidate another's session ──────

    [Fact]
    public async Task Logout_OneDevice_DoesNotInvalidateOtherDevicesSession()
    {
        var client = factory.CreateClient();
        var email = $"twodevice_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Two Device Org {Guid.NewGuid():N}", email);

        var device1 = await LoginAsync(client, org.Organisation.Slug, email, "password123");
        var device2 = await LoginAsync(client, org.Organisation.Slug, email, "password123");

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = device1.RefreshToken }),
        };
        logoutRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", device1.AccessToken);
        var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var device2Refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = device2.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, device2Refresh.StatusCode);

        var device1Refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = device1.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, device1Refresh.StatusCode);
    }

    // ── T047: refresh rotates the token; the old one is rejected on reuse (SC-004) ─

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenRejectedOnReuse()
    {
        var client = factory.CreateClient();
        var email = $"rotate_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Rotate Org {Guid.NewGuid():N}", email);
        var auth = await LoginAsync(client, org.Organisation.Slug, email, "password123");

        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var rotated = (await firstRefresh.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    // ── T048: password reset invalidates every refresh token across all devices ──

    [Fact]
    public async Task ResetPassword_InvalidatesAllRefreshTokensAcrossDevices()
    {
        var client = factory.CreateClient();
        var email = $"resetall_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Reset All Org {Guid.NewGuid():N}", email);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var device1 = await LoginAsync(client, org.Organisation.Slug, email, "password123");
        var device2 = await LoginAsync(client, org.Organisation.Slug, email, "password123");

        var forgotResponse = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { organisationSlug = org.Organisation.Slug, email });
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var token = await GetPasswordResetTokenAsync(schema, email);
        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { organisationSlug = org.Organisation.Slug, token, newPassword = "brand-new-password123" });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var device1Refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = device1.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, device1Refresh.StatusCode);

        var device2Refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = device2.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, device2Refresh.StatusCode);
    }

    // ── T049: refresh and forgot-password both require a resolvable organisation slug ──

    [Fact]
    public async Task Refresh_UnknownOrNotReadyOrganisationSlug_Returns404()
    {
        var client = factory.CreateClient();

        var unknownResponse = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = $"no-such-org-{Guid.NewGuid():N}", refreshToken = "irrelevant" });
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal("errors.auth.organisation_not_found", await GetErrorKeyAsync(unknownResponse));

        var notReadySlug = await SeedNotReadyOrganisationAsync();
        var notReadyResponse = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = notReadySlug, refreshToken = "irrelevant" });
        Assert.Equal(HttpStatusCode.NotFound, notReadyResponse.StatusCode);
        Assert.Equal("errors.auth.organisation_not_found", await GetErrorKeyAsync(notReadyResponse));
    }

    [Fact]
    public async Task ForgotPassword_UnknownOrNotReadyOrganisationSlug_Returns404()
    {
        var client = factory.CreateClient();

        var unknownResponse = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { organisationSlug = $"no-such-org-{Guid.NewGuid():N}", email = "anyone@example.com" });
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal("errors.auth.organisation_not_found", await GetErrorKeyAsync(unknownResponse));

        var notReadySlug = await SeedNotReadyOrganisationAsync();
        var notReadyResponse = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { organisationSlug = notReadySlug, email = "anyone@example.com" });
        Assert.Equal(HttpStatusCode.NotFound, notReadyResponse.StatusCode);
        Assert.Equal("errors.auth.organisation_not_found", await GetErrorKeyAsync(notReadyResponse));
    }

    // ── T050: reset/verify links carry &org=; forgot-password never reveals email existence ──

    [Fact]
    public async Task ForgotPassword_EmailedResetLink_CarriesOrganisationSlug()
    {
        var client = factory.CreateClient();
        var email = $"linkorg_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", email);

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { organisationSlug = org.Organisation.Slug, email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // SMTP isn't configured in tests, so EmailService logs the link instead of sending it
        // (backend/ChildCare.Api/Services/EmailService.cs) — captured here via the same
        // LogCapture sink TenantRejectionTests already uses for server-side-only assertions.
        Assert.Contains(factory.LogCapture.Entries, e =>
            e.Message.Contains("Password reset link") &&
            e.Message.Contains($"org={org.Organisation.Slug}"));
    }

    [Fact]
    public async Task ForgotPassword_RegisteredAndUnregisteredEmail_BothReturn200Identically()
    {
        var client = factory.CreateClient();
        var registeredEmail = $"registered_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Enumeration Org {Guid.NewGuid():N}", registeredEmail);

        var registeredResponse = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { organisationSlug = org.Organisation.Slug, email = registeredEmail });
        var unregisteredResponse = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { organisationSlug = org.Organisation.Slug, email = $"unregistered_{Guid.NewGuid():N}@test.com" });

        Assert.Equal(HttpStatusCode.OK, registeredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unregisteredResponse.StatusCode);
        Assert.Equal(
            await registeredResponse.Content.ReadAsStringAsync(),
            await unregisteredResponse.Content.ReadAsStringAsync());
    }

    // ── T051: invalid/expired reset or verification token uses the i18n error key ──

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsTokenInvalidOrExpiredErrorKey()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invalid Reset Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { organisationSlug = org.Organisation.Slug, token = "not-a-real-token", newPassword = "newpassword123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("errors.auth.token_invalid_or_expired", await GetErrorKeyAsync(response));
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_ReturnsTokenInvalidOrExpiredErrorKey()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invalid Verify Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.PostAsJsonAsync("/api/auth/verify-email",
            new { organisationSlug = org.Organisation.Slug, token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("errors.auth.token_invalid_or_expired", await GetErrorKeyAsync(response));
    }

    // ── T052: the MediatR migration did not drop the existing rate-limit policy ──

    [Fact]
    public void LoginEndpoint_StillDeclaresAuthStrictRateLimitPolicy()
    {
        // Rate limiting middleware itself is disabled in the "Testing" environment (Program.cs)
        // to avoid flaky test failures from rapid-fire integration test traffic, so the
        // meaningful regression check here is structural: the route's EnableRateLimitingAttribute
        // metadata (attached by .RequireRateLimiting(...)) survived the endpoint rewrite,
        // not that it actually throttles under this test host.
        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var loginEndpoint = endpointDataSource.Endpoints.Single(e =>
            e is RouteEndpoint route && route.RoutePattern.RawText == "/api/auth/login");

        var rateLimitMetadata = loginEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        Assert.NotNull(rateLimitMetadata);
        Assert.Equal("auth-strict", rateLimitMetadata.PolicyName);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    private async Task<string> SeedNotReadyOrganisationAsync()
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var slug = $"not-ready-lifecycle-{Guid.NewGuid():N}";
        publicDb.Tenants.Add(new Domain.Entities.Tenant
        {
            Name                    = "Not Ready Lifecycle Org",
            Slug                    = slug,
            SchemaName              = $"tenant_not_ready_lifecycle_{Guid.NewGuid():N}",
            ProvisioningStatus      = ProvisioningStatus.Provisioning,
            CreatedFromInvitationId = Guid.NewGuid(),
        });
        await publicDb.SaveChangesAsync();
        return slug;
    }
}
