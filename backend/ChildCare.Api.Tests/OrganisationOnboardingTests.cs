using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Invitations;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

public class OrganisationOnboardingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private HttpClient CreateClient() => factory.CreateClient();

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

    // ── User Story 1: happy path (spec.md acceptance scenarios 1–4) ────────────────

    [Fact]
    public async Task Register_WithValidInvitation_CreatesReadyOrganisationAndLogsInImmediately()
    {
        var client = CreateClient();
        var invitation = await CreateInvitationAsync(client, "director@example.com");

        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token,
            "Kinderdagverblijf De Zonnebloem",
            "Marie Peeters",
            invitation.Email,
            "correct-horse-battery"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterOrganisationResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken)); // FR-011, SC-002: logged in immediately
        Assert.Equal("trial", body.Organisation.Plan);              // FR-007
        Assert.Equal("Kinderdagverblijf De Zonnebloem", body.Organisation.Name);
        Assert.Equal(invitation.Email, body.Director.Email);
    }

    [Fact]
    public async Task Register_WithoutRegulatoryIdentifiers_Succeeds()
    {
        // FR-012/SC-006: dossiernummer/KBO aren't even part of the request shape —
        // this test documents that omission is the happy path, not an oversight.
        var client = CreateClient();
        var invitation = await CreateInvitationAsync(client, "director2@example.com");

        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token,
            "Kinderdagverblijf Zonneschijn",
            "Jan Janssens",
            invitation.Email,
            "another-strong-password"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
    }

    // ── User Story 2: invite-only enforcement (spec.md acceptance scenarios) ───────

    [Fact]
    public async Task Register_WithExpiredInvitation_ReturnsGenericNotFound()
    {
        // No API surface to expire an invitation deliberately (by design — it's operator-issued,
        // not user-manipulable), so this test inserts an already-expired one directly.
        var (plaintextToken, tokenHash) = InvitationTokenCodec.Generate();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            db.Invitations.Add(new Invitation
            {
                Email = "expired@example.com",
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var client = CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            plaintextToken,
            "Some Org",
            "Some Director",
            "expired@example.com",
            "whatever-password"));

        Assert.Equal(HttpStatusCode.NotFound, registerResponse.StatusCode); // FR-003
        var body = await registerResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("errors.invitation.not_found", body!["errorKey"].ToString());
    }

    [Fact]
    public async Task Register_WithAlreadyUsedInvitation_IsRejectedAsIfNeverIssued()
    {
        var client = CreateClient();
        var invitation = await CreateInvitationAsync(client, "reuse@example.com");

        var firstAttempt = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "First Org", "First Director", invitation.Email, "first-password"));
        Assert.Equal(HttpStatusCode.Created, firstAttempt.StatusCode);

        var secondAttempt = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "Second Org", "Second Director", invitation.Email, "second-password"));

        Assert.Equal(HttpStatusCode.NotFound, secondAttempt.StatusCode); // FR-004 — same outcome as not-found (research.md R5)
    }

    [Fact]
    public async Task Register_WithUnknownInvitationToken_IsRejected()
    {
        var client = CreateClient();

        // Well-formed (same shape a real token would have) but never issued — distinct from
        // the malformed-string case below, both must land on the same generic outcome (FR-005).
        var (neverIssuedToken, _) = InvitationTokenCodec.Generate();
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            neverIssuedToken, "Some Org", "Some Director", "nobody@example.com", "password123"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithMalformedInvitationToken_IsRejected()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            "not-valid-base64url!!!", "Some Org", "Some Director", "nobody@example.com", "password123"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithMismatchedEmail_ReturnsUnprocessableEntity()
    {
        var client = CreateClient();
        var invitation = await CreateInvitationAsync(client, "targeted@example.com");

        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "Some Org", "Some Director", "different-email@example.com", "password123"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); // FR-018, SC-007
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("errors.validation", body!["errorKey"].ToString());
    }

    [Fact]
    public async Task Register_SecondOrganisationWithSameDirectorEmail_Succeeds()
    {
        // spec.md Edge Cases / Assumptions: the same email may plausibly be used to register
        // more than one organisation over time (e.g. a consultant onboarding multiple clients) —
        // this must not be blocked, even though each invitation is still single-use.
        var client = CreateClient();
        const string sharedEmail = "consultant@example.com";

        var firstInvitation = await CreateInvitationAsync(client, sharedEmail);
        var firstResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            firstInvitation.Token, "First Client Org", "Consultant Director", sharedEmail, "password123"));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondInvitation = await CreateInvitationAsync(client, sharedEmail);
        var secondResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            secondInvitation.Token, "Second Client Org", "Consultant Director", sharedEmail, "password456"));

        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<RegisterOrganisationResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<RegisterOrganisationResponse>();
        Assert.NotEqual(firstBody!.Organisation.Id, secondBody!.Organisation.Id); // two distinct organisations
    }
}
