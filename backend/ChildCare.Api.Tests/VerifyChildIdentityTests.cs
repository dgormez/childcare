using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Feature 022, User Stories 1/3/4 (child side): recording, correcting, and role-gating a
/// child's identity verification. See SetChildNrnTests for US5 (the NRN endpoint) and
/// VerifyContactIdentityTests for the contact-side identity-verification equivalent.
/// </summary>
public class VerifyChildIdentityTests(OrganisationOnboardingWebAppFactory factory)
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

    private async Task InsertUserWithRoleAsync(string schemaName, string email, string password, UserRole role)
    {
        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        db.Users.Add(new Domain.Entities.TenantUser
        {
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Name         = $"Test {role}",
            Role         = role,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client, string slug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return body.AccessToken;
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

    // ── T012: happy path sets current + first attribution ────────────────────────

    [Fact]
    public async Task VerifyChildIdentity_HappyPath_SetsCurrentAndFirstAttribution()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("birth_certificate", null)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var verified = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(verified.IdVerifiedAt);
        Assert.Equal("birth_certificate", verified.IdDocumentType);
        Assert.NotNull(verified.IdVerifiedByEmail);
        Assert.Equal(verified.IdVerifiedAt, verified.FirstIdVerifiedAt);
        Assert.Equal(verified.IdVerifiedByEmail, verified.FirstIdVerifiedByEmail);
    }

    // ── T013: missing document type → 422 ────────────────────────────────────────

    [Fact]
    public async Task VerifyChildIdentity_MissingDocumentType_Returns422AndPersistsNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child Missing DocType Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("", null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child.document_type_required", await response.Content.ReadAsStringAsync());

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(reloaded.IdVerifiedAt);
    }

    // ── T014: retroactive verification records today, not the enrolment date ────

    [Fact]
    public async Task VerifyChildIdentity_MonthsAfterEnrolment_TimestampIsNowNotEnrolmentDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child Retro Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var beforeVerify = DateTime.UtcNow;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("passport", "seen original passport")));
        var verified = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;

        Assert.True(verified.IdVerifiedAt >= beforeVerify.AddSeconds(-1));
        Assert.NotEqual(child.CreatedAt, verified.IdVerifiedAt);
    }

    // ── T015: 404 for a non-existent child ───────────────────────────────────────

    [Fact]
    public async Task VerifyChildIdentity_NonExistentChild_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child NotFound Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{Guid.NewGuid()}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("passport", null)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.child.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T015a: non-Director roles get 403 ────────────────────────────────────────

    [Fact]
    public async Task VerifyChildIdentity_NonDirectorRole_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child Role Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        foreach (var token in new[] { staffToken, parentToken })
        {
            var response = await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", token,
                new VerifyChildIdentityRequest("passport", null)));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // ── T015b: verifying a deactivated child still succeeds ──────────────────────

    [Fact]
    public async Task VerifyChildIdentity_DeactivatedChild_StillSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child Deactivated Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("passport", null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var verified = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(verified.IdVerifiedAt);
    }

    // ── T022a: verification/NRN fields are role-gated on shared reads ───────────

    [Fact]
    public async Task GetChild_VerifiedChild_StaffAndDeviceSeeNullVerificationFields_DirectorSeesReal()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child RoleGate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("eid", "seen eID card")));

        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var directorGet = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        var asDirector = (await directorGet.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(asDirector.IdVerifiedAt);
        Assert.Equal("eid", asDirector.IdDocumentType);
        Assert.Equal("seen eID card", asDirector.IdDocumentNote);

        var staffGet = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", staffToken));
        var asStaff = (await staffGet.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(asStaff.IdVerifiedAt);
        Assert.Null(asStaff.IdVerifiedByEmail);
        Assert.Null(asStaff.IdDocumentType);
        Assert.Null(asStaff.IdDocumentNote);
        Assert.Null(asStaff.FirstIdVerifiedAt);
        Assert.Null(asStaff.FirstIdVerifiedByEmail);
        Assert.Null(asStaff.NrnLast4);

        var staffList = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", staffToken));
        var staffListBody = (await staffList.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.All(staffListBody.Where(c => c.Id == child.Id), c => Assert.Null(c.IdVerifiedAt));
    }

    // ── T044: a correction preserves the original first-verification attribution ─

    [Fact]
    public async Task VerifyChildIdentity_Correction_PreservesOriginalFirstAttribution()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Child Correction Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var firstResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("birth_certificate", null)));
        var first = (await firstResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        await Task.Delay(1100); // ensure a distinguishable timestamp on the correction

        var secondResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/identity-verification", org.AccessToken,
            new VerifyChildIdentityRequest("eid", "child turned 12")));
        var second = (await secondResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        Assert.Equal("eid", second.IdDocumentType);
        Assert.True(second.IdVerifiedAt > first.IdVerifiedAt);
        // PostgreSQL timestamptz round-trip precision (a few ticks) — same class of flake
        // IncidentReportImmutabilityTests/RegenerateInvoiceTests/GenerateFiscalAttestations
        // CommandTests already established a millisecond-tolerant comparison for.
        Assert.True(Math.Abs((first.FirstIdVerifiedAt!.Value - second.FirstIdVerifiedAt!.Value).TotalMilliseconds) < 1);
        Assert.Equal(first.FirstIdVerifiedByEmail, second.FirstIdVerifiedByEmail);
        Assert.Equal("birth_certificate", first.IdDocumentType); // sanity on the original snapshot
    }
}
