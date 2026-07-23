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
/// Feature 022, User Stories 2/3 (contact side): recording and correcting a contact's identity
/// verification, independent of which children the contact is linked to. See
/// VerifyChildIdentityTests for the child-side equivalent.
/// </summary>
public class VerifyContactIdentityTests(OrganisationOnboardingWebAppFactory factory)
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
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static async Task<ContactResponse> CreateContactAsync(HttpClient client, string accessToken, string firstName = "Anna") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", "nl"))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    // ── T028: happy path sets current + first attribution ────────────────────────

    [Fact]
    public async Task VerifyContactIdentity_HappyPath_SetsCurrentAndFirstAttribution()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("passport", "seen original passport")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var verified = (await response.Content.ReadFromJsonAsync<ContactResponse>())!;
        Assert.NotNull(verified.IdVerifiedAt);
        Assert.Equal("passport", verified.IdDocumentType);
        Assert.Equal("seen original passport", verified.IdDocumentNote);
        Assert.Equal(verified.IdVerifiedAt, verified.FirstIdVerifiedAt);
        Assert.Equal(verified.IdVerifiedByEmail, verified.FirstIdVerifiedByEmail);
    }

    // ── T029: missing document type → 422 ────────────────────────────────────────

    [Fact]
    public async Task VerifyContactIdentity_MissingDocumentType_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact Missing DocType Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("", null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.contact.document_type_required", await response.Content.ReadAsStringAsync());
    }

    // ── T030: verifying a contact linked to two children shows verified for both ─

    [Fact]
    public async Task VerifyContactIdentity_ContactLinkedToTwoChildren_ShowsVerifiedForBoth()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact Siblings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Lucas");
        var contact = await CreateContactAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childB.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("kids_id", null)));

        var contactsForA = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childA.Id}/contacts", org.AccessToken));
        var listA = (await contactsForA.Content.ReadFromJsonAsync<List<ChildContactResponse>>())!;
        Assert.NotNull(listA.Single(c => c.ContactId == contact.Id).IdVerifiedAt);

        var contactsForB = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childB.Id}/contacts", org.AccessToken));
        var listB = (await contactsForB.Content.ReadFromJsonAsync<List<ChildContactResponse>>())!;
        Assert.NotNull(listB.Single(c => c.ContactId == contact.Id).IdVerifiedAt);
    }

    // ── T031: 404 for a non-existent contact ─────────────────────────────────────

    [Fact]
    public async Task VerifyContactIdentity_NonExistentContact_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact NotFound Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{Guid.NewGuid()}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("passport", null)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.contact.not_found", await response.Content.ReadAsStringAsync());
    }

    // ── T031a: non-Director roles get 403 ────────────────────────────────────────

    [Fact]
    public async Task VerifyContactIdentity_NonDirectorRole_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact Role Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", staffToken,
            new VerifyContactIdentityRequest("passport", null)));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── T031b: verifying an unlinked contact still succeeds ──────────────────────

    [Fact]
    public async Task VerifyContactIdentity_ContactWithNoLinks_StillSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact Unlinked Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("other", null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var verified = (await response.Content.ReadFromJsonAsync<ContactResponse>())!;
        Assert.NotNull(verified.IdVerifiedAt);
    }

    // ── T045: a correction preserves the original first-verification attribution ─

    [Fact]
    public async Task VerifyContactIdentity_Correction_PreservesOriginalFirstAttribution()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Verify Contact Correction Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactAsync(client, org.AccessToken);

        var firstResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("passport", null)));
        var first = (await firstResponse.Content.ReadFromJsonAsync<ContactResponse>())!;

        await Task.Delay(1100);

        var secondResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/contacts/{contact.Id}/identity-verification", org.AccessToken,
            new VerifyContactIdentityRequest("eid", "renewed ID")));
        var second = (await secondResponse.Content.ReadFromJsonAsync<ContactResponse>())!;

        Assert.Equal("eid", second.IdDocumentType);
        Assert.True(second.IdVerifiedAt > first.IdVerifiedAt);
        // PostgreSQL timestamptz round-trip precision (a few ticks) — same class of flake
        // IncidentReportImmutabilityTests/RegenerateInvoiceTests/GenerateFiscalAttestations
        // CommandTests already established a millisecond-tolerant comparison for.
        Assert.True(Math.Abs((first.FirstIdVerifiedAt!.Value - second.FirstIdVerifiedAt!.Value).TotalMilliseconds) < 1);
        Assert.Equal(first.FirstIdVerifiedByEmail, second.FirstIdVerifiedByEmail);
    }
}
