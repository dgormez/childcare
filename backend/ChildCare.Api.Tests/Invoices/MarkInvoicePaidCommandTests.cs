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

/// <summary>Feature 026, tasks.md T029 — MarkInvoicePaidCommand accepts PendingDebit (not just
/// Sent) and rejects any other status (spec.md FR-009).</summary>
public class MarkInvoicePaidCommandTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task MarkPaid_PendingDebitInvoice_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 6);

        var batchResponse = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);

        var markPaidResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(new DateOnly(2027, 6, 20))));

        Assert.Equal(HttpStatusCode.OK, markPaidResponse.StatusCode);
        var updated = (await markPaidResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", updated.Status);
    }

    [Fact]
    public async Task MarkPaid_DraftInvoice_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));

        // Generate but don't send — leaves the invoice in Draft.
        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 6)));
        var draftInvoice = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single(i => i.ChildId == child.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{draftInvoice.Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(new DateOnly(2027, 6, 20))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
