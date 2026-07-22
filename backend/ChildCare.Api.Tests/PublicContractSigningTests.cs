using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ContractSigningTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// Feature 024-esignature, User Story 1 (FR-005 through FR-012, FR-020/FR-021): the public,
/// unauthenticated signing endpoints — reviewing and submitting a signature + SEPA mandate
/// against a token-resolved contract, with no login and no data exposed beyond what the token
/// itself authorizes. Complements ContractSigningTests.cs (the director-facing send/resend half).
/// </summary>
public class PublicContractSigningTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // A real (1x1 transparent pixel) PNG data URI — QuestPdfContractGenerator decodes and
    // embeds a "Drawn" signature's bytes as an actual image, so a placeholder string like
    // "fake" fails DocumentComposeException at PDF-generation time; only a "Typed" signature's
    // SignatureData is free-form text.
    private const string ValidDrawnSignatureDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    // ── T026: GET with a valid token returns contract-for-signing fields; invalid/expired/used → generic 404 ──

    [Fact]
    public async Task GetForSigning_WithValidToken_ReturnsContractFields()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, child, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var response = await GetSigningAsync(client, org.Organisation.Slug, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ContractForSigningResponse>())!;
        Assert.Equal($"{child.FirstName} {child.LastName}", body.ChildName);
        Assert.Equal(3500, body.DailyRateCents);
        Assert.Single(body.ContractedDays);
    }

    [Fact]
    public async Task GetForSigning_WithInvalidToken_ReturnsGenericNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Signing Invalid Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await GetSigningAsync(client, org.Organisation.Slug, "not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.contract_signing.invalid_or_expired", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetForSigning_AfterAlreadySigned_ReturnsGenericNotFound()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var signResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, signResponse.StatusCode);

        var reuseResponse = await GetSigningAsync(client, org.Organisation.Slug, token);
        Assert.Equal(HttpStatusCode.NotFound, reuseResponse.StatusCode);
    }

    // ── T027: a valid POST records signature/mandate fields, generates a PDF, invalidates the token ──

    [Fact]
    public async Task PostSigning_WithValidSignatureAndIban_RecordsEverythingAndInvalidatesToken()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var response = await PostSigningAsync(client, org.Organisation.Slug, token, "Drawn", ValidDrawnSignatureDataUri, ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonElement>());
        Assert.True(body.GetProperty("signed").GetBoolean());

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var stored = await db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        Assert.NotNull(stored.SignedAt);
        Assert.Equal(ValidDrawnSignatureDataUri, stored.SignatureData);
        Assert.False(string.IsNullOrWhiteSpace(stored.SignedByIp));
        Assert.NotNull(stored.SepaMandateReference);
        Assert.NotNull(stored.SepaIbanEncrypted);
        Assert.Equal("7034", stored.SepaIbanLast4);
        Assert.NotNull(stored.SepaAuthorisedAt);
        Assert.Null(stored.SigningToken);
        Assert.Null(stored.SigningTokenExpiresAt);
        Assert.Equal("Draft", stored.Status.ToString()); // FR-015: signing never touches Status

        var storage = factory.Services.GetRequiredService<FakeSignedContractStorage>();
        Assert.True(storage.Uploaded.ContainsKey(contract.Id));

        // Second GET/POST with the same (now-invalidated) token both fail closed (FR-009/FR-012).
        var reuseGet = await GetSigningAsync(client, org.Organisation.Slug, token);
        Assert.Equal(HttpStatusCode.NotFound, reuseGet.StatusCode);
        var reusePost = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.NotFound, reusePost.StatusCode);
    }

    // ── FR-015: signing is not a precondition for Draft → Active — quickstart.md Scenario 1
    // step 6, the other direction from the Status-stays-Draft assertion above ─────────────────

    [Fact]
    public async Task PostSigning_ThenActivate_SucceedsExactlyAsForAnUnsignedContract()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var signResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, signResponse.StatusCode);

        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activated = (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("active", activated.Status);
        Assert.Equal("signed", activated.SigningStatus);
    }

    // ── T028: an invalid-checksum IBAN is rejected and does not consume the token ────────────

    [Fact]
    public async Task PostSigning_WithInvalidChecksumIban_Returns422AndTokenStillUsable()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var badResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", "BE68539007547035");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badResponse.StatusCode);
        Assert.Contains("errors.contract_signing.invalid_iban", await badResponse.Content.ReadAsStringAsync());

        var goodResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, goodResponse.StatusCode);
    }

    // ── T029: concurrent submissions against the same token — exactly one succeeds ──────────

    [Fact]
    public async Task PostSigning_TwoConcurrentSubmissions_ExactlyOneSucceeds()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var first = PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        var second = PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NotFound);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var stored = await db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        Assert.NotNull(stored.SepaMandateReference);

        var storage = factory.Services.GetRequiredService<FakeSignedContractStorage>();
        Assert.True(storage.Uploaded.ContainsKey(contract.Id));
    }

    // ── T030: signed PDF is emailed to both the parent and the director(s) ──────────────────

    [Fact]
    public async Task PostSigning_OnSuccess_EmailsSignedPdfToParentAndDirector()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var response = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var emailSender = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(emailSender.SignedContractCalls, c => c.ToEmail == contactEmail);
        Assert.Contains(emailSender.SignedContractCalls, c => c.ToEmail == org.Director.Email);
    }

    // ── T045: the Pending/Expired boundary (FR-003's 72-hour window) via a controllable clock ──

    [Fact]
    public async Task GetForSigning_AfterExpiry_ReturnsNotFoundAndStatusFlipsToExpired()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        // Still valid before the (simulated) boundary.
        var beforeExpiry = await GetSigningAsync(client, org.Organisation.Slug, token);
        Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var stored = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        stored.SigningTokenExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();

        var afterExpiry = await GetSigningAsync(client, org.Organisation.Slug, token);
        Assert.Equal(HttpStatusCode.NotFound, afterExpiry.StatusCode);

        var directorView = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}", org.AccessToken));
        var directorContract = (await directorView.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("expired", directorContract.SigningStatus);
    }

    // ── Manual quickstart finding: a storage failure after the token-consuming UPDATE must not
    // strand the parent with a burned token and no persisted PDF — the contract is restored to
    // its pre-signed, still-signable state so the exact same emailed link works on retry ──────

    [Fact]
    public async Task PostSigning_WhenPdfStorageFails_RestoresTokenAndLeavesNoPartialState()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var storage = factory.Services.GetRequiredService<FakeSignedContractStorage>();
        storage.ThrowOnUploadFor.Add(contract.Id);

        var failedResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.InternalServerError, failedResponse.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var afterFailure = await db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        Assert.Null(afterFailure.SignedAt);
        Assert.Null(afterFailure.SepaMandateReference);
        Assert.Equal(token, afterFailure.SigningToken);
        Assert.False(storage.Uploaded.ContainsKey(contract.Id));

        // The same link the parent already has still works once storage recovers.
        storage.ThrowOnUploadFor.Remove(contract.Id);
        var retryResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
    }

    // ── A notification failure after a durably-persisted signature must not fail the request or
    // undo the signature (CreateStaffProfileCommandHandler's existing precedent) ─────────────

    [Fact]
    public async Task PostSigning_WhenParentEmailFails_StillSucceedsAndPersistsThePdf()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);
        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);

        var emailSender = factory.Services.GetRequiredService<FakeEmailSender>();
        emailSender.ThrowOnSignedContractEmailTo.Add(contactEmail);

        var response = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var stored = await db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        Assert.NotNull(stored.SignedAt);

        var storage = factory.Services.GetRequiredService<FakeSignedContractStorage>();
        Assert.True(storage.Uploaded.ContainsKey(contract.Id));
    }
}
