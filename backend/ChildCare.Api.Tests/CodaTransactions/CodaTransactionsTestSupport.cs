using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>
/// Shared helpers for feature 025's API test suite — creating a Sent invoice with a real OGM
/// reference, seeding a contract's SEPA IBAN directly (the mandate-capture ceremony itself, from
/// feature 024, isn't what these tests exercise), and uploading a CODA file through
/// FakeCodaParser's test-only sentinel line format (see FakeCodaParser's own doc comment for why
/// a fake is used here instead of hand-authoring the real fixed-width format).
/// </summary>
internal static class CodaTransactionsTestSupport
{
    public static async Task<ContractResponse> CreateAndActivateContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, DateOnly startDate, int dailyRateCents = 4500)
    {
        var request = new CreateContractRequest(
            locationId, startDate, null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))],
            dailyRateCents, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    /// <summary>Directly encrypts and stores a contract's SEPA IBAN via the real IIbanProtector
    /// (feature 024's mechanism) — bypasses the full e-signature ceremony, which isn't what
    /// these tests are exercising.</summary>
    public static async Task SeedContractSepaIbanAsync(IServiceProvider services, string schemaName, Guid contractId, string iban)
    {
        using var scope = services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
        var protector = scope.ServiceProvider.GetRequiredService<IIbanProtector>();
        var db = resolver.ForSchema(schemaName);

        var contract = await db.Contracts.FirstAsync(c => c.Id == contractId);
        var normalized = iban.Replace(" ", string.Empty).ToUpperInvariant();
        contract.SepaIbanEncrypted = protector.Protect(normalized);
        contract.SepaIbanLast4 = normalized[^4..];
        await db.SaveChangesAsync();
    }

    /// <summary>Generates and sends one invoice for the given child/location/month, seeding one
    /// day of Present attendance so the invoice has a non-zero total. Returns the Sent invoice
    /// with its real OgmReference.</summary>
    public static async Task<InvoiceResponse> CreateSentInvoiceAsync(
        HttpClient client, string accessToken, IServiceProvider services, string schemaName,
        Guid childId, Guid locationId, int year, int month, int dailyRateCents = 4500)
    {
        var db = ResolveTenantDb(services, schemaName);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            ChildId = childId,
            LocationId = locationId,
            Date = new DateOnly(year, month, 2),
            Status = Domain.Enums.AttendanceStatus.Present,
            PlannedDurationMinutes = 480,
        });
        await db.SaveChangesAsync();

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        var invoice = invoices.Single(i => i.ChildId == childId);

        var sendResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/invoices/send", accessToken, new SendInvoicesRequest([invoice.Id])));
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var sent = (await sendResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single();
        Assert.Equal("sent", sent.Status);
        return sent;
    }

    /// <summary>The raw 12 structured-communication digits for an invoice's OGM reference —
    /// strips the "+++.../..../...++++" display punctuation (research.md R1).</summary>
    public static string OgmDigits(string ogmReference) => new(ogmReference.Where(char.IsDigit).ToArray());

    public static string FakeCodaLine(DateOnly valueDate, int amountCents, string senderIban, string senderName, string communication, bool isStructured) =>
        $"{valueDate:yyyy-MM-dd}|{amountCents}|{senderIban}|{senderName}|{communication}|{isStructured}";

    public static async Task<HttpResponseMessage> UploadCodaFileAsync(HttpClient client, string accessToken, IEnumerable<string> lines, string fileName = "statement.coda")
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StringContent(string.Join('\n', lines));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/coda-imports") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    public static async Task<List<CodaTransactionResponse>> GetCodaTransactionsAsync(HttpClient client, string accessToken, string? matchType = null, bool? needsReview = null)
    {
        var query = new List<string>();
        if (matchType is not null) query.Add($"matchType={matchType}");
        if (needsReview is not null) query.Add($"needsReview={needsReview.Value.ToString().ToLowerInvariant()}");
        var url = "/api/coda-transactions" + (query.Count > 0 ? "?" + string.Join('&', query) : "");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, url, accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CodaTransactionResponse>>())!;
    }
}
