using System.Net;
using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Email;

/// <summary>User Story 2 (spec.md FR-007/FR-018/FR-020): the no-login digest
/// unsubscribe/resubscribe link.</summary>
public class UnsubscribeEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private string CreateTokenFor(Guid contactId) =>
        factory.Services.GetRequiredService<IUnsubscribeTokenService>().CreateToken(contactId);

    private async Task<DateTime?> GetDigestUnsubscribedAtAsync(string organisationSlug, Guid contactId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Slug == organisationSlug);
        var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
        var contact = await db.Contacts.SingleAsync(c => c.Id == contactId);
        return contact.DigestUnsubscribedAt;
    }

    private HttpClient CreateNonRedirectingClient() =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ── FR-007/FR-020: unsubscribe sets the flag, idempotent, resubscribe clears it ────

    [Fact]
    public async Task Unsubscribe_SetsFlag_IdempotentOnRepeat_ResubscribeClears()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"UnsubIdempotent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var token = CreateTokenFor(contact.Id);

        var firstResponse = await client.PostAsync("/api/email/unsubscribe",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token, ["org"] = org.Organisation.Slug }));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode); // followed the 302 to GET
        Assert.NotNull(await GetDigestUnsubscribedAtAsync(org.Organisation.Slug, contact.Id));

        // Idempotent repeat — same token, already applied — succeeds silently, no error.
        var secondResponse = await client.PostAsync("/api/email/unsubscribe",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token, ["org"] = org.Organisation.Slug }));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(await GetDigestUnsubscribedAtAsync(org.Organisation.Slug, contact.Id));

        var resubscribeResponse = await client.PostAsync("/api/email/resubscribe",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token, ["org"] = org.Organisation.Slug }));
        Assert.Equal(HttpStatusCode.OK, resubscribeResponse.StatusCode);
        Assert.Null(await GetDigestUnsubscribedAtAsync(org.Organisation.Slug, contact.Id));
    }

    [Fact]
    public async Task Unsubscribe_Post_RedirectsBackToGetPage()
    {
        var redirectSetupClient = factory.CreateClient();
        var org = await RegisterOrgAsync(redirectSetupClient, $"UnsubRedirect Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var contact = await CreateContactWithEmailAsync(redirectSetupClient, org.AccessToken);
        var token = CreateTokenFor(contact.Id);

        var client = CreateNonRedirectingClient();
        var response = await client.PostAsync("/api/email/unsubscribe",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token, ["org"] = org.Organisation.Slug }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/api/email/unsubscribe", response.Headers.Location!.ToString());
    }

    // ── FR-018: invalid org / tampered token both fail closed, calmly ──────────────

    [Fact]
    public async Task Get_UnknownOrganisationSlug_ReturnsCalmInvalidPage_Not500()
    {
        var client = factory.CreateClient();
        var token = CreateTokenFor(Guid.NewGuid());

        var response = await client.GetAsync($"/api/email/unsubscribe?token={Uri.EscapeDataString(token)}&org=no-such-organisation-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("niet (meer) geldig", body); // nl fallback InvalidLinkText — no raw error/stack trace
    }

    [Fact]
    public async Task Get_TamperedToken_ReturnsCalmInvalidPage_Not500()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"UnsubTampered Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.GetAsync($"/api/email/unsubscribe?token=not-a-real-token&org={org.Organisation.Slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("niet (meer) geldig", body);
    }

    // ── Constitution Principle I: a token valid for tenant A's contact never resolves ──
    // ── against tenant B's org slug ─────────────────────────────────────────────────

    [Fact]
    public async Task Post_TokenForTenantAContact_WithTenantBOrg_FailsClosed_NeitherContactAffected()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"UnsubTenantA Org {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var contactA = await CreateContactWithEmailAsync(client, orgA.AccessToken);
        var orgB = await RegisterOrgAsync(client, $"UnsubTenantB Org {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var contactB = await CreateContactWithEmailAsync(client, orgB.AccessToken);

        var tokenForA = CreateTokenFor(contactA.Id);

        var response = await client.PostAsync("/api/email/unsubscribe",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = tokenForA, ["org"] = orgB.Organisation.Slug }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // followed redirect, no 500
        Assert.Null(await GetDigestUnsubscribedAtAsync(orgA.Organisation.Slug, contactA.Id)); // tenant A's own contact untouched
        Assert.Null(await GetDigestUnsubscribedAtAsync(orgB.Organisation.Slug, contactB.Id)); // tenant B has no matching contact id either
    }
}
