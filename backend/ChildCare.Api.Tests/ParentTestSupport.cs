using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// Shared HTTP/DB helpers for feature 013's parent-communication test suite — invite/accept/
/// login is a 3-call sequence every US1-US5 test needs, mirroring KioskModeTestSupport's
/// device-pairing helper (feature 008a) for the same reason. Reuses ChildEventTestSupport's
/// CreateChildAsync/CreateContactAsync (feature 009) rather than a second copy.
/// </summary>
internal static class ParentTestSupport
{
    public static async Task<ContactResponse> CreateContactWithEmailAsync(HttpClient client, string accessToken, string? email = null, string firstName = "Anna") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", email ?? $"{Guid.NewGuid():N}@test.com", "nl"))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    public static async Task<HttpResponseMessage> LinkContactAsync(HttpClient client, string accessToken, Guid childId, Guid contactId, bool canPickup = true, string relationship = "Mother") =>
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contacts", accessToken,
            new LinkContactToChildRequest(contactId, relationship, canPickup, true)));

    private static string ExtractLatestParentInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Parent invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    /// <summary>Creates an eligible (CanPickup=true, email on file) contact linked to a fresh
    /// child, invites them to the parent app, accepts, and logs in — returning everything a
    /// parent-authenticated test needs.</summary>
    public static async Task<(ChildResponse Child, ContactResponse Contact, string AccessToken)> InviteAndLoginParentAsync(
        HttpClient client, OrganisationOnboardingWebAppFactory factory, string organisationSlug, string accessToken,
        string? email = null, string password = "password123", string firstName = "Anna")
    {
        var contactEmail = email ?? $"parent_{Guid.NewGuid():N}@test.com";
        var child = await CreateChildAsync(client, accessToken);
        var contact = await CreateContactWithEmailAsync(client, accessToken, contactEmail, firstName);
        await LinkContactAsync(client, accessToken, child.Id, contact.Id);

        var inviteResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", accessToken, new CreateParentInvitationRequest(contact.Id)));
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        var token = ExtractLatestParentInviteToken(factory, contactEmail);
        var acceptResponse = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(organisationSlug, token, password));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug, email = contactEmail, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (child, contact, session.AccessToken);
    }

    /// <summary>Links an already-invited-and-accepted parent to an additional child (shared
    /// family thread scenarios, US2) without a second invitation round-trip.</summary>
    public static async Task<ContactResponse> InviteAndLoginSecondParentForChildAsync(
        HttpClient client, OrganisationOnboardingWebAppFactory factory, string organisationSlug, string accessToken,
        Guid childId, string? email = null, string password = "password123", string firstName = "Bram")
    {
        var contactEmail = email ?? $"parent_{Guid.NewGuid():N}@test.com";
        var contact = await CreateContactWithEmailAsync(client, accessToken, contactEmail, firstName);
        await LinkContactAsync(client, accessToken, childId, contact.Id, relationship: "Father");

        var inviteResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent-invitations", accessToken, new CreateParentInvitationRequest(contact.Id)));
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        var token = ExtractLatestParentInviteToken(factory, contactEmail);
        var acceptResponse = await client.PostAsJsonAsync("/api/parent-invitations/accept",
            new AcceptParentInvitationRequest(organisationSlug, token, password));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        return contact;
    }
}
