using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;
using static ChildCare.Api.Tests.SepaBatches.SepaBatchesTestSupport;
using static ChildCare.Api.Tests.ContractSigningTestSupport;

namespace ChildCare.Api.Tests.Contracts;

/// <summary>Feature 026, tasks.md T040-T042 — User Story 4 (FR-011/FR-012): revoking a signed
/// mandate, rejecting a revoke on a not-yet-signed/already-revoked contract, and confirming
/// feature 024's existing signing-invitation flow still works unchanged afterward.</summary>
public class RevokeSepaMandateTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Revoke_SignedMandate_SetsRevokedStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/revoke-sepa-mandate", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("revoked", updated.MandateStatus);
        Assert.NotNull(updated.SepaRevokedAt);
    }

    [Fact]
    public async Task Revoke_NeverSignedContract_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/revoke-sepa-mandate", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_AlreadyRevokedContract_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var firstRevoke = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/revoke-sepa-mandate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstRevoke.StatusCode);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/revoke-sepa-mandate", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_ThenResendSigningInvitation_SucceedsLikeNeverSigned()
    {
        // Draft contract + primary contact with an email — signing-invitation's own preconditions
        // (FR-001/FR-014/FR-016) require Draft status and a contact email, distinct from
        // eligibility/batch-generation which don't care about contract status at all.
        var (client, org, child, contract) = await SetUpDraftContractWithContactAsync(
            factory.CreateClient(), $"parent_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var revokeResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/revoke-sepa-mandate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var invitationResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/signing-invitation", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, invitationResponse.StatusCode);
    }
}
