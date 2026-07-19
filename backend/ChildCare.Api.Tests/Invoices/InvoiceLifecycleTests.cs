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
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>
/// Feature 014 — spec.md User Story 2 (director reviews, sends, and tracks payment). FR-006
/// (extra charges), FR-007/FR-013 (send, all-or-nothing), FR-009/FR-013 (mark-paid), FR-010
/// (computed overdue).
/// </summary>
public class InvoiceLifecycleTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
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
    public async Task UpdateExtraCharges_OnDraft_RecomputesTotal()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/invoices/{invoice.Id}/extra-charges", org.AccessToken,
            new UpdateInvoiceExtraChargesRequest([new InvoiceExtraChargeRequest("Registration fee", 2500)])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal(invoice.SubtotalCents + 2500, updated.TotalCents);
        Assert.Equal(invoice.SubtotalCents, updated.SubtotalCents);
    }

    [Fact]
    public async Task UpdateExtraCharges_ZeroOrNegativeAmount_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/invoices/{invoice.Id}/extra-charges", org.AccessToken,
            new UpdateInvoiceExtraChargesRequest([new InvoiceExtraChargeRequest("Discount", 0)])));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExtraCharges_OnSentInvoice_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/invoices/{invoice.Id}/extra-charges", org.AccessToken,
            new UpdateInvoiceExtraChargesRequest([new InvoiceExtraChargeRequest("Late fee", 1000)])));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Send_TransitionsDraftToSent_SetsDueDateAndOgm_MakesParentVisible()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        Assert.Null(invoice.DueDate);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = (await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single();
        Assert.Equal("sent", sent.Status);
        Assert.NotNull(sent.SentAt);
        Assert.NotNull(sent.DueDate);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14), sent.DueDate); // default InvoiceDueDays
        Assert.False(string.IsNullOrEmpty(sent.OgmReference));
    }

    [Fact]
    public async Task Send_BatchWithOneNonDraftInvoice_RejectsWholeRequest_ChangesNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        var invoice1 = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child1.Id, 2027, 10);
        var invoice2 = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child2.Id, 2027, 10);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice1.Id])));

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice1.Id, invoice2.Id])));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var stillDraftResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice2.Id}", org.AccessToken));
        var stillDraft = (await stillDraftResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("draft", stillDraft.Status);
    }

    [Fact]
    public async Task MarkPaid_OnSentInvoice_TransitionsToPaid()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        var paidAt = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(paidAt)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paid = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", paid.Status);
        Assert.NotNull(paid.PaidAt);
        Assert.False(paid.IsOverdue);
    }

    // Feature 030 (US3, FR-009a) — marking one invoice of a bundled family group paid cascades
    // to every sibling invoice sharing the same FamilyGroupId in the same transaction.
    [Fact]
    public async Task MarkPaid_OnInvoiceWithFamilyGroupId_CascadesToSiblingInvoices()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/sibling-billing-settings", org.AccessToken,
            new UpdateLocationSiblingBillingSettingsRequest(0, true)));

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child1.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);

        var contractRequest1 = new CreateContractRequest(
            location.Id, new DateOnly(2027, 9, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var contract1 = (await (await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child1.Id}/contracts", org.AccessToken, contractRequest1))).Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract1.Id}/activate", org.AccessToken));

        var contractRequest2 = new CreateContractRequest(
            location.Id, new DateOnly(2027, 9, 8), null,
            [new ContractedDayRequest(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var contract2 = (await (await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child2.Id}/contracts", org.AccessToken, contractRequest2))).Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract2.Id}/activate", org.AccessToken));

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 9)));
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        Assert.All(invoices, i => Assert.NotNull(i.FamilyGroupId));

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest(invoices.Select(i => i.Id).ToList())));

        var paidAt = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoices[0].Id}/mark-paid", org.AccessToken, new MarkInvoicePaidRequest(paidAt)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var siblingResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoices[1].Id}", org.AccessToken));
        var sibling = (await siblingResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", sibling.Status);
        Assert.NotNull(sibling.PaidAt);
    }

    [Fact]
    public async Task MarkPaid_OnDraftInvoice_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SentInvoice_PastDueDate_ReadsAsOverdue()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        // Zero due-days so the invoice is immediately overdue the moment it's sent.
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/invoice-settings", org.AccessToken,
            new UpdateLocationInvoiceSettingsRequest(null, null, 0)));
        var child = await CreateChildAsync(client, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var record = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        record.DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1); // force into the past
        await db.SaveChangesAsync();

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var fetched = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        Assert.True(fetched.IsOverdue);
        Assert.Equal("sent", fetched.Status);
    }
}
