using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>Feature 025, tasks.md T024 — User Story 2 (reject a suggested match).</summary>
public class RejectCodaTransactionMatchTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private const string SenderIban = "BE68539007547034";

    [Fact]
    public async Task Reject_SuggestedMatch_LeavesInvoiceUntouched_MovesToUnmatched()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 8, 1));
        await SeedContractSepaIbanAsync(factory.Services, schema, contract.Id, SenderIban);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 8);

        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 8, 10), invoice.TotalCents, SenderIban, "Test Parent", "thanks!", false)]);
        var suggested = Assert.Single(await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "ibanamount"));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{suggested.Id}/reject", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<CodaTransactionResponse>())!;
        Assert.Equal("unmatched", rejected.MatchType);
        Assert.Null(rejected.MatchedInvoice);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("sent", updatedInvoice.Status);

        var unmatched = await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "unmatched");
        Assert.Contains(unmatched, t => t.Id == suggested.Id);
    }

    [Fact]
    public async Task Reject_NonSuggestedTransaction_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 8, 1), 1234, SenderIban, "Nobody", "no match", false)]);
        var unmatched = Assert.Single(await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "unmatched"));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/coda-transactions/{unmatched.Id}/reject", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
