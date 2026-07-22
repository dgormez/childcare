using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;
using static ChildCare.Api.Tests.SepaBatches.SepaBatchesTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>Feature 026, tasks.md T032-T035 — User Story 3 (FR-010): a returned debit reverts a
/// PendingDebit invoice to Sent with a reason, never touches a Paid one, and the returned
/// invoice is eligible for a later batch exactly like any other Sent invoice.</summary>
public class MarkInvoiceSepaReturnedTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient client, RegisterOrganisationResponse org, LocationResponse location, string schema, ContractResponse contract, InvoiceResponse invoice)> SetupPendingDebitInvoiceAsync(int month = 6)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, month, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, month);

        var batchResponse = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);

        return (client, org, location, schema, contract, invoice);
    }

    [Fact]
    public async Task MarkReturned_PendingDebitInvoice_RevertsToSent_WithReason()
    {
        var (client, org, _, _, _, invoice) = await SetupPendingDebitInvoiceAsync();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-sepa-returned", org.AccessToken,
            new MarkInvoiceSepaReturnedRequest("Insufficient funds")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("sent", updated.Status);
        Assert.Null(updated.SepaBatchId);
        Assert.Equal("Insufficient funds", updated.SepaReturnReason);
    }

    [Fact]
    public async Task MarkReturned_SentInvoice_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 6);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-sepa-returned", org.AccessToken,
            new MarkInvoiceSepaReturnedRequest("Insufficient funds")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MarkReturned_PaidInvoice_Returns422()
    {
        var (client, org, _, _, _, invoice) = await SetupPendingDebitInvoiceAsync();
        var markPaidResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(new DateOnly(2027, 6, 20))));
        Assert.Equal(HttpStatusCode.OK, markPaidResponse.StatusCode);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-sepa-returned", org.AccessToken,
            new MarkInvoiceSepaReturnedRequest("Insufficient funds")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MarkReturned_EmptyReason_Returns422()
    {
        var (client, org, _, _, _, invoice) = await SetupPendingDebitInvoiceAsync();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-sepa-returned", org.AccessToken,
            new MarkInvoiceSepaReturnedRequest("")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ReturnedInvoice_IsEligibleForALaterBatch_LikeAnyOtherSentInvoice()
    {
        var (client, org, location, schema, _, invoice) = await SetupPendingDebitInvoiceAsync();
        var returnResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-sepa-returned", org.AccessToken,
            new MarkInvoiceSepaReturnedRequest("Insufficient funds")));
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);

        var eligibility = await GetEligibilityAsync(client, org.AccessToken, location.Id, 2027, 6);

        Assert.Single(eligibility.Eligible, e => e.InvoiceId == invoice.Id);
    }
}
