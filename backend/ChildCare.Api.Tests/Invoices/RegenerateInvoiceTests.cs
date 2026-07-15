using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>
/// Feature 014 — spec.md User Story 4 (director regenerates after correcting attendance).
/// FR-011 (recompute, preserve OgmReference/SentAt/DueDate, re-notify if sent), FR-012 (paid is
/// immutable — a rejected attempt leaves every field, including UpdatedAt, untouched).
/// </summary>
public class RegenerateInvoiceTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<(InvoiceResponse Invoice, Guid ChildId)> CreateDraftInvoiceAsync(
        HttpClient client, string accessToken, Guid locationId, int year, int month)
    {
        var child = await CreateChildAsync(client, accessToken);
        var request = new CreateContractRequest(
            locationId, new DateOnly(year, month, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        return (invoices.Single(i => i.ChildId == child.Id), child.Id);
    }

    [Fact]
    public async Task Regenerate_OnDraft_RecomputesLineItems()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (invoice, childId) = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, 2027, 9);
        Assert.Equal(0, invoice.LineItems.PresentDays);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = childId, LocationId = location.Id, Date = new DateOnly(2027, 9, 6), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invoices/{invoice.Id}/regenerate", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var regenerated = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal(1, regenerated.LineItems.PresentDays);
        Assert.Equal(3500, regenerated.SubtotalCents);
    }

    [Fact]
    public async Task Regenerate_OnSent_PreservesOgmSentAtDueDate_AndRecomputesTotals()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (invoice, childId) = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, 2027, 9);
        var sendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));
        var sent = (await sendResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single();

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = childId, LocationId = location.Id, Date = new DateOnly(2027, 9, 6), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invoices/{invoice.Id}/regenerate", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var regenerated = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("sent", regenerated.Status);
        Assert.Equal(sent.OgmReference, regenerated.OgmReference);
        // Tolerant, not exact, equality — PostgreSQL's timestamptz round-trip precision can differ
        // from .NET's in-memory DateTime by a few ticks (same recurring class of flakiness this
        // codebase's other timestamp-equality assertions already work around, e.g.
        // IncidentReportImmutabilityTests/DeactivateVaccineTypeTests).
        Assert.True(Math.Abs((sent.SentAt!.Value - regenerated.SentAt!.Value).TotalMilliseconds) < 1);
        Assert.Equal(sent.DueDate, regenerated.DueDate);
        Assert.Equal(1, regenerated.LineItems.PresentDays);
    }

    [Fact]
    public async Task Regenerate_OnPaidInvoice_Returns422_AndLeavesEveryFieldUnchanged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (invoice, _) = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));
        var paidResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));
        var paid = (await paidResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var beforeAttempt = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invoices/{invoice.Id}/regenerate", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.invoice.paid_immutable", await response.Content.ReadAsStringAsync());

        var afterAttempt = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(beforeAttempt.UpdatedAt, afterAttempt.UpdatedAt);
        Assert.Equal(beforeAttempt.LineItems, afterAttempt.LineItems);
        Assert.Equal(beforeAttempt.TotalCents, afterAttempt.TotalCents);
        Assert.Equal("paid", paid.Status);
    }
}
