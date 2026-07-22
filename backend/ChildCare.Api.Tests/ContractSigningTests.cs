using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ContractSigningTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>
/// Feature 024-esignature, User Stories 2/3 (FR-001/FR-004/FR-013/FR-016/FR-018): director sends
/// (or resends) a signing invitation from a Draft contract, and a resend immediately invalidates
/// the previously issued link. Complements PublicContractSigningTests.cs (the parent-facing half
/// of the same flow) and the signed-contract edit-lock test in ContractLifecycleTests.cs.
/// </summary>
public class ContractSigningTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T018: sending sets SigningToken/SigningTokenExpiresAt and sends an email ────────────

    [Fact]
    public async Task SendSigningInvitation_OnDraftWithContactEmail_SendsEmailAndSetsPendingStatus()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/signing-invitation", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("pending", updated.SigningStatus);

        var emailSender = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(emailSender.ContractSigningInvitationCalls, c => c.ToEmail == contactEmail);
    }

    // ── T019: 422 when contact has no email, and when creditor ID isn't configured ──────────

    [Fact]
    public async Task SendSigningInvitation_WithNoContactEmail_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Signing NoContact Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await SetCreditorIdAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateDraftContractAsync(client, org.AccessToken, child.Id, location.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/signing-invitation", org.AccessToken));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.contract_signing.no_contact_email", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SendSigningInvitation_WithoutCreditorIdConfigured_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Signing NoCreditor Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        await LinkPrimaryContactAsync(client, org.AccessToken, child.Id, $"parent_{Guid.NewGuid():N}@test.com");
        var contract = await CreateDraftContractAsync(client, org.AccessToken, child.Id, location.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/signing-invitation", org.AccessToken));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.contract_signing.creditor_id_not_configured", await response.Content.ReadAsStringAsync());
    }

    // ── T020: 409 for a non-Draft contract ───────────────────────────────────────────────────

    [Fact]
    public async Task SendSigningInvitation_OnActiveContract_Returns409()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);

        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/signing-invitation", org.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.contract.not_draft", await response.Content.ReadAsStringAsync());
    }

    // ── T038 (US3): resending issues a new token and immediately invalidates the previous one ──

    [Fact]
    public async Task ResendSigningInvitation_InvalidatesPreviousLink()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);

        var firstToken = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);
        var secondToken = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);
        Assert.NotEqual(firstToken, secondToken);

        var oldTokenGet = await GetSigningAsync(client, org.Organisation.Slug, firstToken);
        Assert.Equal(HttpStatusCode.NotFound, oldTokenGet.StatusCode);

        var newTokenGet = await GetSigningAsync(client, org.Organisation.Slug, secondToken);
        Assert.Equal(HttpStatusCode.OK, newTokenGet.StatusCode);
    }

    // ── FR-018/US2 Acceptance Scenario 3: the persisted signed PDF is reachable once signed,
    // and not before (converge finding — no endpoint previously exposed it) ──────────────────

    [Fact]
    public async Task GetSignedPdfUrl_BeforeSigning_ReturnsNotFound_AfterSigning_ReturnsUrl()
    {
        var contactEmail = $"parent_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var (_, org, _, contract) = await SetUpDraftContractWithContactAsync(client, contactEmail);

        var beforeSigning = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/signed-pdf-url", org.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, beforeSigning.StatusCode);

        var token = await SendInvitationAndExtractTokenAsync(factory.Services, client, org.AccessToken, contract.Id, contactEmail);
        var signResponse = await PostSigningAsync(client, org.Organisation.Slug, token, "Typed", "Parent Peeters", ValidTestIban);
        Assert.Equal(HttpStatusCode.OK, signResponse.StatusCode);

        var afterSigning = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contract.Id}/signed-pdf-url", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, afterSigning.StatusCode);
        var body = await afterSigning.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains($"signed-contracts/{contract.Id}.pdf", body.GetProperty("downloadUrl").GetString());
    }
}
