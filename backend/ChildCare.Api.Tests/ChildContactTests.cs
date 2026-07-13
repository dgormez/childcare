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
/// User Story 2 (SC-004): a director links contacts to a child, shares a contact across
/// siblings without duplication, and manages the primary-contact invariant (FR-007), including
/// its auto-promotion-on-unlink fix (CHK005).
/// </summary>
public class ChildContactTests(OrganisationOnboardingWebAppFactory factory)
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
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static async Task<ContactResponse> CreateContactAsync(HttpClient client, string accessToken, string firstName = "Anna") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", "nl"))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    private int CountContactRows(string schemaName)
    {
        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        return db.Contacts.Count();
    }

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    // ── T044/T047: link a contact, first link forced primary ─────────────────────

    [Fact]
    public async Task LinkContactToChild_FirstLink_ForcedPrimary()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contact Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactAsync(client, org.AccessToken);

        var linkRequest = new LinkContactToChildRequest(contact.Id, "Mother", true, false);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken, linkRequest));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var link = (await response.Content.ReadFromJsonAsync<ChildContactResponse>())!;
        Assert.True(link.IsPrimary); // forced true despite request saying false
    }

    // ── T045/SC-004: shared contact across siblings, no duplication ─────────────

    [Fact]
    public async Task LinkSameContact_ToSiblingChild_NoDuplicateContactRowCreated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Contact Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Liam");
        var contact = await CreateContactAsync(client, org.AccessToken);

        var contactCountBefore = CountContactRows(schema);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child1.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));
        var secondLinkResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child2.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));

        Assert.Equal(HttpStatusCode.Created, secondLinkResponse.StatusCode);
        Assert.Equal(contactCountBefore, CountContactRows(schema)); // no new Contact row
    }

    // ── T046: updating a shared contact reflects on every linked child ──────────

    [Fact]
    public async Task UpdateContact_ReflectsOnAllLinkedChildren()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contact Update Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Liam");
        var contact = await CreateContactAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child1.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child2.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));

        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/contacts/{contact.Id}", org.AccessToken,
            new UpdateContactRequest("Anna", "Peeters", "+32 9 999 99 99", contact.Email, "fr")));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = (await updateResponse.Content.ReadFromJsonAsync<ContactResponse>())!;
        Assert.Equal("+32 9 999 99 99", updated.Phone);
        Assert.Equal("fr", updated.Locale);
    }

    // ── T048/CHK005: designating a new primary clears (not deletes) the old link ─

    [Fact]
    public async Task DesignateNewPrimary_ClearsOldPrimary_WithoutDeletingLink()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Primary Swap Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact1 = await CreateContactAsync(client, org.AccessToken, "Anna");
        var contact2 = await CreateContactAsync(client, org.AccessToken, "Tom");

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact1.Id, "Mother", true, true)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact2.Id, "Father", true, false)));

        var updateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/children/{child.Id}/contacts/{contact2.Id}", org.AccessToken,
            new UpdateChildContactLinkRequest("Father", true, true)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schema);
        var contact1Link = db.ChildContacts.First(cc => cc.ChildId == child.Id && cc.ContactId == contact1.Id);
        Assert.False(contact1Link.IsPrimary); // cleared, not deleted
    }

    // ── T049: unlink removes the link, not the underlying contact ───────────────

    [Fact]
    public async Task UnlinkContact_RemovesLinkOnly_ContactAndOtherLinksRemain()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Unlink Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Liam");
        var contact = await CreateContactAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child1.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child2.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));

        var unlinkResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{child1.Id}/contacts/{contact.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);

        // Contact still resolvable, and still linked to child2.
        var contactStillExists = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/contacts", org.AccessToken));
        var contacts = (await contactStillExists.Content.ReadFromJsonAsync<List<ContactResponse>>())!;
        Assert.Contains(contacts, c => c.Id == contact.Id);
    }

    // ── T050: zero contacts → empty list, no error ───────────────────────────────

    [Fact]
    public async Task Child_WithZeroContacts_NoErrorOnGet()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Zero Contacts Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── T092/CHK005: unlinking the primary auto-promotes the remaining contact ──

    [Fact]
    public async Task UnlinkPrimaryContact_WithAnotherRemaining_AutoPromotesReplacement()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AutoPromote Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact1 = await CreateContactAsync(client, org.AccessToken, "Anna");
        var contact2 = await CreateContactAsync(client, org.AccessToken, "Tom");

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact1.Id, "Mother", true, true))); // primary
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact2.Id, "Father", true, false)));

        var unlinkResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{child.Id}/contacts/{contact1.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);

        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schema);
        var remainingLink = db.ChildContacts.First(cc => cc.ChildId == child.Id && cc.ContactId == contact2.Id);
        Assert.True(remainingLink.IsPrimary);
    }

    // ── T093/CHK005/CHK012: unlinking the only (primary) contact — no error, nothing to promote ─

    [Fact]
    public async Task UnlinkOnlyContact_NoErrorAndNoPromotionAttempted()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"UnlinkOnly Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contacts", org.AccessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));

        var unlinkResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{child.Id}/contacts/{contact.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
