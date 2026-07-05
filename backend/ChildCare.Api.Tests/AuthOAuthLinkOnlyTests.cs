using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 2: Google/Apple sign-in links to an existing account only — it never creates one
/// (FR-009, closing an open-registration path discovered during /speckit-plan's codebase
/// review) — and rejects a sign-in method not permitted for the matched account's role
/// (FR-017). Google/Apple token validation is swapped for FakeGoogleTokenValidator/
/// FakeAppleTokenValidator (research.md R7's ports), so these tests control token validity
/// deterministically without a real provider round-trip (quickstart.md Scenario 2 & 4).
/// </summary>
public class AuthOAuthLinkOnlyTests(OrganisationOnboardingWebAppFactory factory)
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

    private async Task<int> CountUsersAsync(string schemaName)
    {
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        return await db.Users.CountAsync();
    }

    /// <summary>Directly inserts a TenantUser with the given role (bypassing the normal flow —
    /// no staff/parent provisioning feature exists yet) so FR-017's role-gating can be tested.</summary>
    private async Task<string> InsertUserWithRoleAsync(string schemaName, string email, UserRole role)
    {
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        db.Users.Add(new Domain.Entities.TenantUser
        {
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("irrelevant"),
            Name         = $"Test {role}",
            Role         = role,
        });
        await db.SaveChangesAsync();
        return email;
    }

    private static async Task<string> GetErrorKeyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["errorKey"].ToString()!;
    }

    // ── T030: Google sign-in with no matching account never creates one ────────

    [Fact]
    public async Task GoogleSignIn_ValidTokenNoMatchingAccount_Returns401AndCreatesNoAccount()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Google NoMatch Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var usersBefore = await CountUsersAsync(schema);

        var fakeGoogle = factory.Services.GetRequiredService<FakeGoogleTokenValidator>();
        var token = $"fake-google-{Guid.NewGuid():N}";
        var nonMatchingEmail = $"nomatch_{Guid.NewGuid():N}@test.com";
        fakeGoogle.Behavior = t => t == token ? new GoogleIdentity("google-sub-nomatch", nonMatchingEmail) : null;

        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/google",
                new { organisationSlug = org.Organisation.Slug, idToken = token });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("errors.auth.invalid_credentials", await GetErrorKeyAsync(response));
            Assert.Equal(usersBefore, await CountUsersAsync(schema)); // no account was created
        }
        finally
        {
            fakeGoogle.Behavior = null;
        }
    }

    // ── T031: Google sign-in links to an existing, unlinked account ─────────────

    [Fact]
    public async Task GoogleSignIn_ValidTokenMatchingExistingAccount_LinksAndReturns200()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Google Link Org {Guid.NewGuid():N}", directorEmail);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var fakeGoogle = factory.Services.GetRequiredService<FakeGoogleTokenValidator>();
        var token = $"fake-google-{Guid.NewGuid():N}";
        fakeGoogle.Behavior = t => t == token ? new GoogleIdentity("google-sub-link", directorEmail) : null;

        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/google",
                new { organisationSlug = org.Organisation.Slug, idToken = token });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
            Assert.NotNull(body);
            Assert.NotEmpty(body.AccessToken);

            var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schema);
            var user = await db.Users.SingleAsync(u => u.Email == directorEmail);
            Assert.Equal("google-sub-link", user.GoogleId);
        }
        finally
        {
            fakeGoogle.Behavior = null;
        }
    }

    // ── T067 (convergence): Google sign-in is also allowed for Parent-role accounts ──

    [Fact]
    public async Task GoogleSignIn_AgainstParentRoleAccount_LinksAndReturns200()
    {
        // FR-017's table allows Google for both Director and Parent (only Staff is denied) —
        // GoogleSignIn_ValidTokenMatchingExistingAccount_LinksAndReturns200 above only exercises
        // the Director case; this closes the gap found by /speckit-converge.
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Google Parent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var parentEmail = await InsertUserWithRoleAsync(schema, $"parent_{Guid.NewGuid():N}@test.com", UserRole.Parent);

        var fakeGoogle = factory.Services.GetRequiredService<FakeGoogleTokenValidator>();
        var token = $"fake-google-{Guid.NewGuid():N}";
        fakeGoogle.Behavior = t => t == token ? new GoogleIdentity("google-sub-parent", parentEmail) : null;

        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/google",
                new { organisationSlug = org.Organisation.Slug, idToken = token });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schema);
            var user = await db.Users.SingleAsync(u => u.Email == parentEmail);
            Assert.Equal("google-sub-parent", user.GoogleId);
        }
        finally
        {
            fakeGoogle.Behavior = null;
        }
    }

    // ── T032: Apple first-time sign-in requires the client-supplied email to link ──

    [Fact]
    public async Task AppleSignIn_FirstTimeWithClientSuppliedEmail_LinksExistingParentAccount()
    {
        // Apple sign-in is a parent-app-only method (FR-017) — the account under test must be
        // Parent-role, not the org's auto-created Director (that combination is covered, and
        // expected to be rejected, by AppleSignIn_AgainstDirectorRoleAccount_Returns403 below).
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Apple Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var parentEmail = await InsertUserWithRoleAsync(schema, $"parent_{Guid.NewGuid():N}@test.com", UserRole.Parent);

        var fakeApple = factory.Services.GetRequiredService<FakeAppleTokenValidator>();
        var token = $"fake-apple-{Guid.NewGuid():N}";
        // Apple's token itself carries no email on this (simulated) first sign-in — the client
        // supplies it in the request body instead, matching Apple's real first-sign-in behavior.
        fakeApple.Behavior = (t, _) => t == token ? new AppleIdentity("apple-sub-link", null) : null;

        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/apple",
                new { organisationSlug = org.Organisation.Slug, identityToken = token, email = parentEmail });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schema);
            var user = await db.Users.SingleAsync(u => u.Email == parentEmail);
            Assert.Equal("apple-sub-link", user.AppleId);
        }
        finally
        {
            fakeApple.Behavior = null;
        }
    }

    // ── T033: sign-in method not permitted for the matched account's role (FR-017) ──

    [Fact]
    public async Task GoogleSignIn_AgainstStaffRoleAccount_Returns403MethodNotAllowed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Google Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var staffEmail = await InsertUserWithRoleAsync(schema, $"staff_{Guid.NewGuid():N}@test.com", UserRole.Staff);

        var fakeGoogle = factory.Services.GetRequiredService<FakeGoogleTokenValidator>();
        var token = $"fake-google-{Guid.NewGuid():N}";
        fakeGoogle.Behavior = t => t == token ? new GoogleIdentity("google-sub-staff", staffEmail) : null;

        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/google",
                new { organisationSlug = org.Organisation.Slug, idToken = token });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("errors.auth.method_not_allowed_for_role", await GetErrorKeyAsync(response));
        }
        finally
        {
            fakeGoogle.Behavior = null;
        }
    }

    [Fact]
    public async Task AppleSignIn_AgainstDirectorRoleAccount_Returns403MethodNotAllowed()
    {
        // Web admin (Director) is password + Google only — Apple sign-in is a parent-app-only
        // method (FR-017), so even a fully valid Apple token must be refused for a Director.
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Director Apple Org {Guid.NewGuid():N}", directorEmail);

        var fakeApple = factory.Services.GetRequiredService<FakeAppleTokenValidator>();
        var token = $"fake-apple-{Guid.NewGuid():N}";
        fakeApple.Behavior = (t, _) => t == token ? new AppleIdentity("apple-sub-director", directorEmail) : null;

        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/apple",
                new { organisationSlug = org.Organisation.Slug, identityToken = token, email = directorEmail });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("errors.auth.method_not_allowed_for_role", await GetErrorKeyAsync(response));
        }
        finally
        {
            fakeApple.Behavior = null;
        }
    }

    // ── T034: invalid tokens rejected; not-ready organisation rejected too ──────

    [Fact]
    public async Task GoogleAndAppleSignIn_InvalidTokens_RejectedWithoutIssuingSession()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invalid Token Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        // Fakes default to returning null (no Behavior configured) for any unrecognized token —
        // simulating a tampered/expired/wrong-audience token that fails provider validation.
        var googleResponse = await client.PostAsJsonAsync("/api/auth/google",
            new { organisationSlug = org.Organisation.Slug, idToken = "tampered-or-expired-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, googleResponse.StatusCode);
        Assert.Equal("errors.auth.invalid_credentials", await GetErrorKeyAsync(googleResponse));

        var appleResponse = await client.PostAsJsonAsync("/api/auth/apple",
            new { organisationSlug = org.Organisation.Slug, identityToken = "tampered-or-expired-token", email = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, appleResponse.StatusCode);
        Assert.Equal("errors.auth.invalid_credentials", await GetErrorKeyAsync(appleResponse));
    }

    [Fact]
    public async Task GoogleAndAppleSignIn_OrganisationNotReady_Returns404BeforeTokenValidation()
    {
        string slug;
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
            slug = $"not-ready-oauth-{Guid.NewGuid():N}";
            publicDb.Tenants.Add(new Domain.Entities.Tenant
            {
                Name                    = "Not Ready OAuth Org",
                Slug                    = slug,
                SchemaName              = $"tenant_not_ready_oauth_{Guid.NewGuid():N}",
                ProvisioningStatus      = ProvisioningStatus.Provisioning,
                CreatedFromInvitationId = Guid.NewGuid(),
            });
            await publicDb.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        var googleResponse = await client.PostAsJsonAsync("/api/auth/google",
            new { organisationSlug = slug, idToken = "irrelevant-never-validated" });
        Assert.Equal(HttpStatusCode.NotFound, googleResponse.StatusCode);
        Assert.Equal("errors.auth.organisation_not_found", await GetErrorKeyAsync(googleResponse));

        var appleResponse = await client.PostAsJsonAsync("/api/auth/apple",
            new { organisationSlug = slug, identityToken = "irrelevant-never-validated", email = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, appleResponse.StatusCode);
        Assert.Equal("errors.auth.organisation_not_found", await GetErrorKeyAsync(appleResponse));
    }
}
