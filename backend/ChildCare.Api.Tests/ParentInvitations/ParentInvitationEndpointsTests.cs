using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests.ParentInvitations;

/// <summary>
/// User Story 0 (SC-000): a director invites an eligible contact to the parent app, the
/// invitee accepts and logs in. Mirrors StaffProfileCrudTests' invite/accept structure
/// (feature 005) — see specs/013-parent-communication/research.md R1.
/// </summary>
public class ParentInvitationEndpointsTests(OrganisationOnboardingWebAppFactory factory)
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
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    // "__random__" (not literal null) means "no explicit email supplied by the caller — generate
    // one" — a real ?? on a nullable parameter can't distinguish "explicitly no email" (the
    // no-email test case) from "caller didn't pass one", so a sentinel default is used instead.
    private const string RandomEmail = "__random__";

    private static async Task<ContactResponse> CreateContactAsync(HttpClient client, string accessToken, string? email = RandomEmail, string firstName = "Anna") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", email == RandomEmail ? $"{Guid.NewGuid():N}@test.com" : email, "nl"))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    private static async Task LinkContactAsync(HttpClient client, string accessToken, Guid childId, Guid contactId, bool canPickup = true) =>
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contacts", accessToken,
            new LinkContactToChildRequest(contactId, "Mother", canPickup, true)));

    /// <summary>Creates an eligible (CanPickup=true, email on file) contact linked to a fresh child.</summary>
    private static async Task<(ChildResponse Child, ContactResponse Contact)> CreateEligibleContactAsync(
        HttpClient client, string accessToken, string? email = null)
    {
        var child = await CreateChildAsync(client, accessToken);
        var contact = await CreateContactAsync(client, accessToken, email);
        await LinkContactAsync(client, accessToken, child.Id, contact.Id);
        return (child, contact);
    }

    private static string ExtractLatestParentInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Parent invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    // ── T028: director invites an eligible contact ──────────────────────────────

    [Fact]
    public async Task CreateParentInvitation_EligibleContact_ReturnsCreated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invite Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var (_, contact) = await CreateEligibleContactAsync(client, org.AccessToken, contactEmail);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invitation = (await response.Content.ReadFromJsonAsync<ParentInvitationResponse>())!;
        Assert.Equal(contact.Id, invitation.ContactId);
        Assert.Equal(contactEmail, invitation.Email);

        var token = ExtractLatestParentInviteToken(factory, contactEmail);
        Assert.False(string.IsNullOrEmpty(token));
    }

    // ── T029: not eligible — no email, or CanPickup=false ───────────────────────

    [Fact]
    public async Task CreateParentInvitation_ContactWithNoEmail_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NoEmail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactAsync(client, org.AccessToken, email: null);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.parent_invitation.not_eligible", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateParentInvitation_NotCanPickup_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NoPickup Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id, canPickup: false);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.parent_invitation.not_eligible", await response.Content.ReadAsStringAsync());
    }

    // ── T030: already has an account → 409 ──────────────────────────────────────

    [Fact]
    public async Task CreateParentInvitation_ContactAlreadyHasAccount_Returns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AlreadyHasAccount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var (_, contact) = await CreateEligibleContactAsync(client, org.AccessToken, contactEmail);

        var firstInvite = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));
        Assert.Equal(HttpStatusCode.Created, firstInvite.StatusCode);
        var token = ExtractLatestParentInviteToken(factory, contactEmail);
        var acceptResponse = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(org.Organisation.Slug, token, "password123"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var secondInvite = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));

        Assert.Equal(HttpStatusCode.Conflict, secondInvite.StatusCode);
        Assert.Contains("errors.parent_invitation.already_has_account", await secondInvite.Content.ReadAsStringAsync());
    }

    // ── T031: accept flow sets PasswordHash + Contact.TenantUserId, login succeeds ──

    [Fact]
    public async Task AcceptParentInvitation_ThenLogin_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Accept Parent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var (_, contact) = await CreateEligibleContactAsync(client, org.AccessToken, contactEmail);

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));
        var token = ExtractLatestParentInviteToken(factory, contactEmail);

        var acceptResponse = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(org.Organisation.Slug, token, "newpassword123"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email = contactEmail, password = "newpassword123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schema);
        var updatedContact = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
        Assert.NotNull(updatedContact.TenantUserId);
    }

    // ── T032: expired and already-used tokens both return generic 404 ──────────

    [Fact]
    public async Task AcceptParentInvitation_UnknownToken_ReturnsGenericNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BadToken Parent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(org.Organisation.Slug, "not-a-real-token", "password123"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.invitation.not_found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AcceptParentInvitation_Twice_SecondAttemptReturnsGenericNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DoubleAccept Parent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var (_, contact) = await CreateEligibleContactAsync(client, org.AccessToken, contactEmail);

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));
        var token = ExtractLatestParentInviteToken(factory, contactEmail);

        var firstAccept = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(org.Organisation.Slug, token, "password123"));
        Assert.Equal(HttpStatusCode.OK, firstAccept.StatusCode);

        var secondAccept = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(org.Organisation.Slug, token, "differentpassword"));
        Assert.Equal(HttpStatusCode.NotFound, secondAccept.StatusCode);
        Assert.Contains("errors.invitation.not_found", await secondAccept.Content.ReadAsStringAsync());
    }

    // ── T033/FR-006a: accepting backfills existing thread participation ─────────

    [Fact]
    public async Task AcceptParentInvitation_BackfillsExistingThreadParticipation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Backfill Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var (child, contact) = await CreateEligibleContactAsync(client, org.AccessToken, contactEmail);

        // Seed a thread for the child directly (messaging endpoints ship in a later story) —
        // this test only proves the invitation-accept side of FR-006a's backfill.
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schema);
        var thread = new Domain.Entities.MessageThread { Subject = "Existing thread", ChildId = child.Id };
        db.MessageThreads.Add(thread);
        await db.SaveChangesAsync();

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", org.AccessToken, new CreateParentInvitationRequest(contact.Id)));
        var token = ExtractLatestParentInviteToken(factory, contactEmail);
        await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(org.Organisation.Slug, token, "password123"));

        var updatedContact = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
        var isParticipant = await db.MessageThreadParticipants
            .AnyAsync(p => p.ThreadId == thread.Id && p.TenantUserId == updatedContact.TenantUserId);
        Assert.True(isParticipant);
    }

    // ── T034: invite action unavailable for a contact with no email ────────────
    // (No web UI exists yet in this backend test suite — covered by the web component test,
    // T034's own file; the backend-side guarantee this depends on is CHK002/FR-000a above.)

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }
}
