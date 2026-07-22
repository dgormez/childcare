using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// Shared HTTP/DB helpers for feature 024-esignature's test suite (ContractSigningTests —
/// director-side send/resend, PublicContractSigningTests — parent-side signing). Setting up a
/// signable Draft contract (org + creditor ID + location + child + primary contact + contract)
/// is a 6-call sequence repeated by both files, substantial enough to share (mirrors
/// KioskModeTestSupport's own "device pairing is substantial enough" precedent) — unlike
/// ContractLifecycleTests.cs's simpler single-purpose helpers, which stay local to that file.
/// </summary>
internal static class ContractSigningTestSupport
{
    /// <summary>A syntactically/checksum-valid Belgian test IBAN (mod-97 verified).</summary>
    public const string ValidTestIban = "BE68539007547034";

    // RegisterOrgAsync/AuthedRequest/GetSchemaNameAsync/ResolveTenantDb are shared from
    // KioskModeTestSupport (identical shape) rather than duplicated here.

    public static async Task SetCreditorIdAsync(HttpClient client, string accessToken, string? creditorId = "BE68ZZZ0123456789")
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/organisations/me", accessToken, new UpdateOrganisationRequest(null, creditorId)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest("Main Building", "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    public static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    public static async Task<ContractResponse> CreateDraftContractAsync(HttpClient client, string accessToken, Guid childId, Guid locationId)
    {
        var request = new CreateContractRequest(
            locationId, new DateOnly(2026, 1, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))],
            3500, null);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    /// <summary>Creates and links a primary contact with the given email — the addressee a
    /// signing invitation resolves via the IsPrimary-ordered ChildContact join.</summary>
    public static async Task LinkPrimaryContactAsync(HttpClient client, string accessToken, Guid childId, string email)
    {
        var contactResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken, new CreateContactRequest("Parent", "Peeters", "+32 470 00 00 00", email, "nl")));
        Assert.Equal(HttpStatusCode.Created, contactResponse.StatusCode);
        var contact = (await contactResponse.Content.ReadFromJsonAsync<ContactResponse>())!;

        var linkResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contacts", accessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, true)));
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);
    }

    /// <summary>Registers an org with a SEPA Creditor Identifier already configured, a location,
    /// a child with a primary contact at <paramref name="contactEmail"/>, and a Draft contract —
    /// the common starting point every send/sign test needs. Caller supplies its own
    /// <paramref name="client"/> (from <c>factory.CreateClient()</c>).</summary>
    public static async Task<(HttpClient Client, RegisterOrganisationResponse Org, ChildResponse Child, ContractResponse Contract)>
        SetUpDraftContractWithContactAsync(HttpClient client, string contactEmail)
    {
        var org = await RegisterOrgAsync(client, $"Signing Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await SetCreditorIdAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        await LinkPrimaryContactAsync(client, org.AccessToken, child.Id, contactEmail);
        var contract = await CreateDraftContractAsync(client, org.AccessToken, child.Id, location.Id);
        return (client, org, child, contract);
    }

    /// <summary>Sends a signing invitation and extracts the token from the emailed signing URL —
    /// the only way a test can obtain a real, valid signing token (research.md R2: the token is
    /// opaque Data-Protection ciphertext, not derivable from the contract id).</summary>
    public static async Task<string> SendInvitationAndExtractTokenAsync(
        IServiceProvider services, HttpClient client, string accessToken, Guid contractId, string contactEmail)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contractId}/signing-invitation", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var emailSender = services.GetRequiredService<FakeEmailSender>();
        var signingUrl = emailSender.ContractSigningInvitationCalls.Last(c => c.ToEmail == contactEmail).SigningUrl;
        return QueryHelpers.ParseQuery(new Uri(signingUrl).Query)["token"].ToString();
    }

    public static Task<HttpResponseMessage> GetSigningAsync(HttpClient client, string orgSlug, string token) =>
        client.GetAsync($"/api/public/contracts/sign?org={orgSlug}&token={Uri.EscapeDataString(token)}");

    public static Task<HttpResponseMessage> PostSigningAsync(
        HttpClient client, string orgSlug, string token, string signatureType, string signatureData, string sepaIban) =>
        client.PostAsJsonAsync(
            $"/api/public/contracts/sign?org={orgSlug}&token={Uri.EscapeDataString(token)}",
            new SubmitContractSigningRequest(signatureType, signatureData, sepaIban));
}
