using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>Feature 025, tasks.md T023/T025 — User Story 2 (confirm a suggested match).</summary>
public class ConfirmCodaTransactionMatchTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private const string SenderIban = "BE68539007547034";

    private async Task<(HttpClient Client, string AccessToken, InvoiceResponse Invoice)> SetupOpenInvoiceWithSepaMandateAsync(int year, int month)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(year, month, 1));
        await SeedContractSepaIbanAsync(factory.Services, schema, contract.Id, SenderIban);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, year, month);
        return (client, org.AccessToken, invoice);
    }

    [Fact]
    public async Task Confirm_SuggestedMatch_MarksInvoicePaid()
    {
        var (client, accessToken, invoice) = await SetupOpenInvoiceWithSepaMandateAsync(2027, 8);

        // No structured reference, but amount+sender match the invoice's contract exactly.
        await UploadCodaFileAsync(client, accessToken,
            [FakeCodaLine(new DateOnly(2027, 8, 10), invoice.TotalCents, SenderIban, "Test Parent", "thanks!", false)]);

        var suggested = await GetCodaTransactionsAsync(client, accessToken, matchType: "ibanamount");
        var row = Assert.Single(suggested);
        Assert.False(row.Applied);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{row.Id}/confirm", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmed = (await response.Content.ReadFromJsonAsync<CodaTransactionResponse>())!;
        Assert.True(confirmed.Applied);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", accessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", updatedInvoice.Status);
    }

    [Fact]
    public async Task Confirm_StaleAlreadyPaidInvoice_ReSurfacesAsDuplicate_NeverApplies()
    {
        var (client, accessToken, invoice) = await SetupOpenInvoiceWithSepaMandateAsync(2027, 9);

        await UploadCodaFileAsync(client, accessToken,
            [FakeCodaLine(new DateOnly(2027, 9, 10), invoice.TotalCents, SenderIban, "Test Parent", "thanks!", false)]);
        var suggested = Assert.Single(await GetCodaTransactionsAsync(client, accessToken, matchType: "ibanamount"));

        // Mark the invoice paid through a different path in the meantime (director manual action).
        var markPaidResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", accessToken, new MarkInvoicePaidRequest(new DateOnly(2027, 9, 11))));
        Assert.Equal(HttpStatusCode.OK, markPaidResponse.StatusCode);

        var confirmResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{suggested.Id}/confirm", accessToken));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmResponse.StatusCode);

        var duplicates = await GetCodaTransactionsAsync(client, accessToken, matchType: "duplicate");
        Assert.Contains(duplicates, t => t.Id == suggested.Id);
    }

    [Fact]
    public async Task Confirm_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{Guid.NewGuid()}/confirm", org.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_AlreadyAppliedTransaction_Returns422()
    {
        var (client, accessToken, invoice) = await SetupOpenInvoiceWithSepaMandateAsync(2027, 10);
        await UploadCodaFileAsync(client, accessToken,
            [FakeCodaLine(new DateOnly(2027, 10, 10), invoice.TotalCents, SenderIban, "Test Parent", "thanks!", false)]);
        var suggested = Assert.Single(await GetCodaTransactionsAsync(client, accessToken, matchType: "ibanamount"));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{suggested.Id}/confirm", accessToken));

        var secondConfirm = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{suggested.Id}/confirm", accessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondConfirm.StatusCode);
    }
}
