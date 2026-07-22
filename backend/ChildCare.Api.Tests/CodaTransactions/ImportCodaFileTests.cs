using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;
using static ChildCare.Api.Tests.SepaBatches.SepaBatchesTestSupport;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>
/// Feature 025, tasks.md T011-T016/T015a — User Story 1 (import + auto-reconcile).
/// FakeCodaParser (registered in OrganisationOnboardingWebAppFactory) stands in for the real
/// CODA parsing library — see its own doc comment for why.
/// </summary>
public class ImportCodaFileTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Import_ExactOgmMatch_MarksInvoicePaid_AndReportsSummary()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 3, 1));
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 3);

        var lines = new[]
        {
            FakeCodaLine(new DateOnly(2027, 3, 15), invoice.TotalCents, "BE68539007547034", "Test Parent", OgmDigits(invoice.OgmReference), true),
            FakeCodaLine(new DateOnly(2027, 3, 16), 999999, "BE71096123456769", "Nobody", "no reference here", false),
        };

        var response = await UploadCodaFileAsync(client, org.AccessToken, lines);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;

        Assert.Equal(2, summary.TransactionCount);
        Assert.Equal(0, summary.SkippedDuplicateCount);
        Assert.Equal(1, summary.Summary.Ogm);
        Assert.Equal(1, summary.Summary.Unmatched);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", updatedInvoice.Status);
        Assert.Equal(new DateOnly(2027, 3, 15), updatedInvoice.PaidAt.HasValue ? DateOnly.FromDateTime(updatedInvoice.PaidAt.Value) : default);
    }

    [Fact]
    public async Task Import_MalformedFile_Returns422_NoRowsPersisted()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await UploadCodaFileAsync(client, org.AccessToken, ["this-is-not-a-valid-sentinel-line"]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var transactions = await GetCodaTransactionsAsync(client, org.AccessToken);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task Import_ReuploadSameFile_SkipsAlreadyImportedTransactions()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var lines = new[] { FakeCodaLine(new DateOnly(2027, 3, 20), 5000, "BE68539007547034", "Someone", "free text", false) };

        var first = await UploadCodaFileAsync(client, org.AccessToken, lines);
        var firstSummary = (await first.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;
        Assert.Equal(0, firstSummary.SkippedDuplicateCount);

        var second = await UploadCodaFileAsync(client, org.AccessToken, lines);
        var secondSummary = (await second.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;
        Assert.Equal(1, secondSummary.SkippedDuplicateCount);
        Assert.Equal(1, secondSummary.TransactionCount);

        var transactions = await GetCodaTransactionsAsync(client, org.AccessToken);
        Assert.Single(transactions);
    }

    [Fact]
    public async Task Import_OgmMatchAgainstAlreadyPaidInvoice_IsDuplicate_NeverReapplied()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);
        var ogmDigits = OgmDigits(invoice.OgmReference);

        // First transaction pays the invoice in full.
        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 4, 10), invoice.TotalCents, "BE68539007547034", "Test Parent", ogmDigits, true)]);

        // A second transaction referencing the same (now-paid) invoice arrives in a SEPARATE import.
        var secondResponse = await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 4, 11), invoice.TotalCents, "BE68539007547034", "Test Parent", ogmDigits, true)]);
        var secondSummary = (await secondResponse.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;
        Assert.Equal(1, secondSummary.Summary.Duplicate);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        // Still shows the FIRST payment's date — never overwritten by the duplicate.
        Assert.Equal(new DateOnly(2027, 4, 10), DateOnly.FromDateTime(updatedInvoice.PaidAt!.Value));
    }

    [Fact]
    public async Task Import_SameImport_TwoLinesReferenceSameInvoice_SecondIsDuplicate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 5, 1));
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 5);
        var ogmDigits = OgmDigits(invoice.OgmReference);

        // Two distinct transactions (different value dates) in the SAME file both name this invoice.
        var lines = new[]
        {
            FakeCodaLine(new DateOnly(2027, 5, 10), invoice.TotalCents, "BE68539007547034", "Test Parent", ogmDigits, true),
            FakeCodaLine(new DateOnly(2027, 5, 11), invoice.TotalCents, "BE68539007547034", "Test Parent", ogmDigits, true),
        };

        var response = await UploadCodaFileAsync(client, org.AccessToken, lines);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;

        Assert.Equal(1, summary.Summary.Ogm);
        Assert.Equal(1, summary.Summary.Duplicate);
    }

    [Fact]
    public async Task Import_PartialPayment_DoesNotMarkInvoicePaid_TracksReceivedAmount()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 6);
        var ogmDigits = OgmDigits(invoice.OgmReference);
        var partialAmount = invoice.TotalCents / 2;

        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 6, 12), partialAmount, "BE68539007547034", "Test Parent", ogmDigits, true)]);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("sent", updatedInvoice.Status);

        var transactions = await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "ogm");
        var row = Assert.Single(transactions);
        Assert.False(row.Applied);
        Assert.Equal(partialAmount, row.MatchedInvoice!.ReceivedCents);
        Assert.Equal(invoice.TotalCents, row.MatchedInvoice.TotalCents);
    }

    [Fact]
    public async Task Import_TwoPartialPayments_CombinedMeetTotal_MarksInvoicePaidOnSecond()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 7, 1));
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 7);
        var ogmDigits = OgmDigits(invoice.OgmReference);
        var firstAmount = invoice.TotalCents / 2;
        var secondAmount = invoice.TotalCents - firstAmount;

        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 7, 10), firstAmount, "BE68539007547034", "Test Parent", ogmDigits, true)]);

        var getAfterFirst = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        Assert.Equal("sent", (await getAfterFirst.Content.ReadFromJsonAsync<InvoiceResponse>())!.Status);

        await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 7, 20), secondAmount, "BE68539007547034", "Test Parent", ogmDigits, true)]);

        var getAfterSecond = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var final = (await getAfterSecond.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", final.Status);
        Assert.Equal(new DateOnly(2027, 7, 20), DateOnly.FromDateTime(final.PaidAt!.Value));
    }

    [Fact]
    public async Task Import_AmountIbanCoincidentallyMatchesEarlierPaidInvoice_IsClosedInvoice_NeverReopened()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 1, 1));
        const string senderIban = "BE68539007547034";
        await SeedContractSepaIbanAsync(factory.Services, schema, contract.Id, senderIban);

        // An earlier-period invoice, already paid (e.g. via a prior manual mark-paid).
        var earlierInvoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 1);
        var markPaidResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{earlierInvoice.Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(new DateOnly(2027, 1, 20))));
        Assert.Equal(HttpStatusCode.OK, markPaidResponse.StatusCode);

        // A later transaction whose amount+sender coincidentally match that now-closed invoice,
        // with no structured reference naming it and no open invoice to suggest instead.
        var response = await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 4, 5), earlierInvoice.TotalCents, senderIban, "Test Parent", "late payment?", false)]);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;
        Assert.Equal(1, summary.Summary.ClosedInvoice);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{earlierInvoice.Id}", org.AccessToken));
        var stillPaid = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", stillPaid.Status);
        Assert.Equal(new DateOnly(2027, 1, 20), DateOnly.FromDateTime(stillPaid.PaidAt!.Value));

        var closedInvoiceRows = await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "closedinvoice");
        Assert.Single(closedInvoiceRows);
    }

    [Fact]
    public async Task Import_NegativeAmount_IsReversal_NeverMatched()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 3, 5), -1500, "BE68539007547034", "Refund Sender", "reversal", false)]);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;

        Assert.Equal(1, summary.Summary.Reversal);
        var transactions = await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "reversal");
        var row = Assert.Single(transactions);
        Assert.Null(row.MatchedInvoice);
    }

    [Fact]
    public async Task Import_TransactionCountParity_EverythingAccountedFor()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var lines = new[]
        {
            FakeCodaLine(new DateOnly(2027, 3, 1), 1000, "BE68539007547034", "A", "x", false),
            FakeCodaLine(new DateOnly(2027, 3, 2), 2000, "BE71096123456769", "B", "y", false),
            FakeCodaLine(new DateOnly(2027, 3, 3), -500, "BE71096123456769", "C", "z", false),
        };

        var response = await UploadCodaFileAsync(client, org.AccessToken, lines);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;

        var s = summary.Summary;
        var sumOfCategories = s.Ogm + s.IbanAmountSuggested + s.Unmatched + s.Duplicate + s.ClosedInvoice + s.Reversal + summary.SkippedDuplicateCount;
        Assert.Equal(summary.TransactionCount, sumOfCategories);
    }

    // ── Feature 026, tasks.md T027/T028 — a PendingDebit invoice (from a generated SEPA batch)
    // is just as open/matchable to CODA reconciliation as a Sent one (spec.md FR-009). ──

    [Fact]
    public async Task Import_ExactOgmMatch_AgainstPendingDebitInvoice_MarksItPaid()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
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
        var pendingDebitCheck = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        Assert.Equal("pendingdebit", (await pendingDebitCheck.Content.ReadFromJsonAsync<InvoiceResponse>())!.Status);

        var response = await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 6, 15), invoice.TotalCents, "BE68539007547034", "Test Parent", OgmDigits(invoice.OgmReference), true)]);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;
        Assert.Equal(1, summary.Summary.Ogm);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", updatedInvoice.Status);
    }

    [Fact]
    public async Task Import_AmountIbanMatch_AgainstPendingDebitInvoice_IsOfferedAsSuggestion()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Coda Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 6, 1));
        const string debtorIban = "BE71096123456769";
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, debtorIban);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 6);

        var batchResponse = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);

        var response = await UploadCodaFileAsync(client, org.AccessToken,
            [FakeCodaLine(new DateOnly(2027, 6, 20), invoice.TotalCents, debtorIban, "Test Parent", "no reference", false)]);
        var summary = (await response.Content.ReadFromJsonAsync<CodaImportSummaryResponse>())!;
        Assert.Equal(1, summary.Summary.IbanAmountSuggested);

        var suggested = await GetCodaTransactionsAsync(client, org.AccessToken, matchType: "ibanamount");
        var row = Assert.Single(suggested);
        Assert.Equal(invoice.Id, row.MatchedInvoice?.Id);
    }
}
