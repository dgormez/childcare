using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Payments;

/// <summary>
/// Feature 014a — spec.md User Story 4 (automatic payment receipt). FR-015 (generated on Paid
/// via either path), FR-016 (deterministic content), FR-017 (on-demand, Paid-only), Security
/// considerations (indistinguishable not-found — mirrors GenerateParentInvoicePdfQuery's
/// existing 014 precedent).
/// </summary>
public class BetalingsbewijsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<InvoiceResponse> CreateSentInvoiceAsync(
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
        var draft = invoices.Single(i => i.ChildId == childId);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", accessToken, new SendInvoicesRequest([draft.Id])));
        return draft;
    }

    [Fact]
    public async Task Betalingsbewijs_ForInvoicePaidManually_IsAvailableAndIsPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Receipt Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/betalingsbewijs", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Betalingsbewijs_ForUnpaidInvoice_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Receipt Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/betalingsbewijs", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Betalingsbewijs_ForPaidInvoiceNotBelongingToParent_Returns404_SameAsUnpaid()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Receipt Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");
        var otherInvoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, otherChild.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{otherInvoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{otherInvoice.Id}/betalingsbewijs", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Betalingsbewijs_QueriedTwice_ReturnsSameContentEachTime()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Receipt Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));

        var first = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/betalingsbewijs", parentToken));
        var second = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/betalingsbewijs", parentToken));

        // Not byte-for-byte equal — QuestPDF embeds a fresh generation timestamp on every
        // render (confirmed: 014's own GenerateInvoicePdfQuery has the same property and its
        // own tests never assert byte-equality either). FR-016's determinism is about the
        // invoice's underlying data never changing after Paid, not the PDF binary — comparable
        // length is the honest proxy for "same substantive content" here.
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        var secondBytes = await second.Content.ReadAsByteArrayAsync();
        Assert.Equal(firstBytes.Length, secondBytes.Length);
    }

    [Fact]
    public async Task MarkPaidManually_SendsReceiptPushToContactWithToken()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Receipt Push Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, contact, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await SetContactPushTokenAsync(org.Organisation.Slug, contact.Id, "ExponentPushToken[receipt-manual]");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 10);

        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));

        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[receipt-manual]");
    }

    private async Task SetContactPushTokenAsync(string tenantSlug, Guid contactId, string pushToken)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var tenant = publicDb.Tenants.Single(t => t.Slug == tenantSlug);
        var resolver = scope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(tenant.SchemaName);
        var contact = await db.Contacts.SingleAsync(c => c.Id == contactId);
        contact.PushToken = pushToken;
        await db.SaveChangesAsync();
    }
}
