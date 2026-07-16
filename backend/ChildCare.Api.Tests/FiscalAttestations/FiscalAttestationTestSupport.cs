using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.FiscalAttestations;

/// <summary>Feature 015 — shared seeding helpers for fiscal-attestation tests. Billable days
/// come from real AttendanceRecord rows (BillableDayCalculator, 014), not from a contract's
/// weekday pattern alone — seeded directly via ITenantDbContext, mirroring
/// GenerateInvoicesTests' precedent, rather than through the full device check-in ceremony.</summary>
public static class FiscalAttestationTestSupport
{
    private static DateOnly FirstOccurrenceOnOrAfter(DateOnly start, DayOfWeek weekday)
    {
        var offset = ((int)weekday - (int)start.DayOfWeek + 7) % 7;
        return start.AddDays(offset);
    }

    /// <summary>Creates an active contract at the given daily rate, seeds one Present attendance
    /// record for (year, month), generates + sends + marks paid an invoice — the minimal unit an
    /// aggregator/attestation test needs a "Paid invoice" with a nonzero amount to exist. Returns
    /// the paid InvoiceResponse.</summary>
    public static async Task<InvoiceResponse> CreatePaidInvoiceAsync(
        HttpClient client, string accessToken, IServiceProvider services, Guid organisationId,
        Guid locationId, Guid childId, int year, int month,
        int dailyRateCents = 3500, DayOfWeek weekday = DayOfWeek.Monday)
    {
        await CreateAndActivateContractAsync(client, accessToken, locationId, childId, new DateOnly(year, month, 1), null, dailyRateCents, weekday);
        return await CreatePaidInvoiceForExistingContractAsync(client, accessToken, services, organisationId, locationId, childId, year, month, weekday);
    }

    /// <summary>Same as CreatePaidInvoiceAsync, but reuses an existing (already-activated)
    /// contract at a specific location — needed when a test seeds two contracts at different
    /// daily rates for the same child (a mid-year rate change), or an additional paid month
    /// against the same open-ended contract.</summary>
    public static async Task<InvoiceResponse> CreatePaidInvoiceForExistingContractAsync(
        HttpClient client, string accessToken, IServiceProvider services, Guid organisationId,
        Guid locationId, Guid childId, int year, int month, DayOfWeek weekday = DayOfWeek.Monday)
    {
        var schema = await GetSchemaNameAsync(services, organisationId);
        var db = ResolveTenantDb(services, schema);
        var attendanceDate = FirstOccurrenceOnOrAfter(new DateOnly(year, month, 1), weekday);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            ChildId = childId,
            LocationId = locationId,
            Date = attendanceDate,
            Status = AttendanceStatus.Present,
            PlannedDurationMinutes = 480,
        });
        await db.SaveChangesAsync();

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        var invoice = invoices.Single(i => i.ChildId == childId);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", accessToken, new SendInvoicesRequest([invoice.Id])));
        var paidAt = new DateOnly(year, month, 28);
        var markPaidResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", accessToken, new MarkInvoicePaidRequest(paidAt)));
        return (await markPaidResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    public static async Task<ContractResponse> CreateAndActivateContractAsync(
        HttpClient client, string accessToken, Guid locationId, Guid childId, DateOnly startDate, DateOnly? endDate, int dailyRateCents, DayOfWeek weekday = DayOfWeek.Monday)
    {
        var contractRequest = new CreateContractRequest(
            locationId, startDate, endDate,
            [new ContractedDayRequest(weekday, new TimeOnly(8, 0), new TimeOnly(17, 0))], dailyRateCents, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, contractRequest));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        return contract;
    }

    /// <summary>Amends an active contract to a new daily rate effective a given date — the
    /// mechanism a mid-year rate change actually goes through (research.md, feature 007's
    /// AmendContractCommand), rather than two independently-created overlapping contracts.</summary>
    public static async Task<ContractResponse> AmendContractRateAsync(
        HttpClient client, string accessToken, Guid contractId, Guid locationId, DateOnly effectiveStartDate, int newDailyRateCents, DayOfWeek weekday = DayOfWeek.Monday)
    {
        var request = new AmendContractRequest(
            effectiveStartDate, locationId, null,
            [new ContractedDayRequest(weekday, new TimeOnly(8, 0), new TimeOnly(17, 0))], newDailyRateCents, null);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contractId}/amend", accessToken, request));
        return (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
    }
}
