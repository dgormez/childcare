using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 4 (spec.md FR-009/FR-010): current-month invoice status overview reusing
/// the existing Status/DueDate overdue convention (research.md R6), and tenant isolation.</summary>
public class InvoiceStatusOverviewEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> GetOverviewAsync(HttpClient client, string accessToken) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/reports/invoices", accessToken));

    private static async Task<InvoiceResponse> CreateDraftInvoiceAsync(
        HttpClient client, string accessToken, Guid locationId, Guid childId, int year, int month)
    {
        var request = new CreateContractRequest(
            locationId, new DateOnly(year, month, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        return invoices.Single(i => i.ChildId == childId);
    }

    [Fact]
    public async Task InvoiceOverview_BucketsPaidOutstandingOverdue_WithCorrectTotals()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Overview Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Paid invoice.
        var paidChild = await CreateChildAsync(client, org.AccessToken, "PaidChild");
        var paidInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, paidChild.Id, today.Year, today.Month);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([paidInvoice.Id])));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invoices/{paidInvoice.Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(today)));

        // Outstanding (sent, not yet due).
        var outstandingChild = await CreateChildAsync(client, org.AccessToken, "OutstandingChild");
        var outstandingInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, outstandingChild.Id, today.Year, today.Month);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([outstandingInvoice.Id])));

        // Overdue: sent, then backdate DueDate directly (no way to make real time pass in a test).
        var overdueChild = await CreateChildAsync(client, org.AccessToken, "OverdueChild");
        var overdueInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, overdueChild.Id, today.Year, today.Month);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([overdueInvoice.Id])));

        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var trackedOverdue = await db.Invoices.FirstAsync(i => i.Id == overdueInvoice.Id);
        trackedOverdue.DueDate = today.AddDays(-17);
        await db.SaveChangesAsync();

        var response = await GetOverviewAsync(client, org.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<InvoiceStatusOverviewResponse>())!;

        Assert.Equal(1, body.PaidCount);
        Assert.Equal(1, body.OutstandingCount);
        Assert.Equal(1, body.OverdueCount);
        Assert.Equal(body.PaidTotalCents + body.OutstandingTotalCents + body.OverdueTotalCents, body.TotalInvoicedCents);

        var overdueRow = Assert.Single(body.OverdueInvoices, i => i.InvoiceId == overdueInvoice.Id);
        Assert.Equal(17, overdueRow.DaysOverdue);
    }

    [Fact]
    public async Task InvoiceOverview_NoOverdueInvoices_ReturnsEmptyList()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Overview Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await CreateLocationAsync(client, org.AccessToken, "Main");

        var response = await GetOverviewAsync(client, org.AccessToken);
        var body = (await response.Content.ReadFromJsonAsync<InvoiceStatusOverviewResponse>())!;
        Assert.Empty(body.OverdueInvoices);
        Assert.Equal(0, body.OverdueCount);
    }

    [Fact]
    public async Task InvoiceOverview_CrossTenant_NeverLeaksOtherTenantsInvoices()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Tenant A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, orgA.AccessToken, "Location A");
        var childA = await CreateChildAsync(client, orgA.AccessToken, "TenantAChild");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceA = await CreateDraftInvoiceAsync(client, orgA.AccessToken, locationA.Id, childA.Id, today.Year, today.Month);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", orgA.AccessToken, new SendInvoicesRequest([invoiceA.Id])));

        var orgB = await RegisterOrgAsync(client, $"Tenant B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var responseB = await GetOverviewAsync(client, orgB.AccessToken);
        var bodyB = (await responseB.Content.ReadFromJsonAsync<InvoiceStatusOverviewResponse>())!;

        Assert.Equal(0, bodyB.OutstandingCount + bodyB.PaidCount + bodyB.OverdueCount);
        Assert.DoesNotContain(bodyB.OverdueInvoices, i => i.InvoiceId == invoiceA.Id);
    }
}
