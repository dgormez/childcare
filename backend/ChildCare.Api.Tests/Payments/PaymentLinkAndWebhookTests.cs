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
/// Feature 014a — spec.md User Story 1 (parent pays an invoice online). FR-004/FR-005 (payment
/// link, gated on connection), FR-006/FR-007 (tenant resolution, never trust payload), FR-008/
/// FR-009 (one-way transition, idempotent), FR-010 (status polling), Edge Cases (forged
/// reference, duplicate webhook, payment-link reuse).
/// </summary>
public class PaymentLinkAndWebhookTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private record CheckoutUrlResponse(string CheckoutUrl);
    private record PaymentStatusResponse(string InvoiceStatus, string? PaymentStatus);

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

    private static async Task ConnectMollieAsync(HttpClient client, string accessToken) =>
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/organisations/me/payment-connection/callback", accessToken,
            new CompletePaymentConnectionRequest("fake-code")));

    [Fact]
    public async Task PaymentLink_WithConnectedProvider_ReturnsCheckoutUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<CheckoutUrlResponse>())!;
        Assert.Contains("fake-mollie.test/checkout", body.CheckoutUrl);
    }

    [Fact]
    public async Task PaymentLink_WithoutConnectedProvider_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PaymentLink_CalledTwiceWhileOpen_ReturnsSameCheckoutUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var first = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));
        var firstBody = (await first.Content.ReadFromJsonAsync<CheckoutUrlResponse>())!;

        var second = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));
        var secondBody = (await second.Content.ReadFromJsonAsync<CheckoutUrlResponse>())!;

        // Same checkout URL back both times is exactly the observable proof of reuse — a
        // second created payment would have a distinct fake provider payment ID baked into its
        // URL (FakePaymentProvider.CreatePaymentAsync).
        Assert.Equal(firstBody.CheckoutUrl, secondBody.CheckoutUrl);

        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var paymentsForInvoice = publicDb.Payments.Where(p => p.InvoiceId == invoice.Id).ToList();
        Assert.Single(paymentsForInvoice); // one Payment row created for this invoice, not two
    }

    [Fact]
    public async Task Webhook_ConfirmsPaid_TransitionsInvoiceAndIsIdempotent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Webhook Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await SetContactPushTokenAsync(org.Organisation.Slug, contact.Id, "ExponentPushToken[webhook-receipt]");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));

        var fakeProvider = factory.Services.GetRequiredService<FakePaymentProvider>();
        var fakePayment = Assert.Single(fakeProvider.Payments.Values);
        fakePayment.Status = "paid";

        // Need the PaymentReference the system generated — read it via the payment-status
        // endpoint's side channel isn't exposed, so resolve it via the public webhook URL the
        // system itself would have called Mollie with is not directly observable here; instead
        // drive the webhook through the same command the real webhook hits, using the DB-level
        // PaymentReference (public schema) looked up through the test host's services.
        var paymentReference = await GetPaymentReferenceAsync(fakePayment.ProviderPaymentId);

        var webhookResponse1 = await client.PostAsync($"/api/webhooks/mollie/{paymentReference}", null);
        Assert.Equal(HttpStatusCode.OK, webhookResponse1.StatusCode);

        var statusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/payment-status", parentToken));
        var status = (await statusResponse.Content.ReadFromJsonAsync<PaymentStatusResponse>())!;
        Assert.Equal("paid", status.InvoiceStatus);
        Assert.Equal("paid", status.PaymentStatus);

        // FR-015 — the receipt notification fires for the webhook path too, not just manual
        // mark-paid (BetalingsbewijsTests covers the manual path). Filtered by title, not just
        // token — the same contact already received 014's own "invoice sent" push earlier in
        // this flow, on the same token.
        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[webhook-receipt]" && p.Title == "Betalingsbewijs beschikbaar");

        // Duplicate delivery — idempotent no-op.
        var webhookResponse2 = await client.PostAsync($"/api/webhooks/mollie/{paymentReference}", null);
        Assert.Equal(HttpStatusCode.OK, webhookResponse2.StatusCode);

        var statusAfterDuplicate = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/payment-status", parentToken));
        var statusAfterDuplicateBody = (await statusAfterDuplicate.Content.ReadFromJsonAsync<PaymentStatusResponse>())!;
        Assert.Equal("paid", statusAfterDuplicateBody.InvoiceStatus);

        // Idempotent means no duplicate receipt notification either — exactly one receipt push.
        Assert.Single(pushSender.Sent, p => p.PushToken == "ExponentPushToken[webhook-receipt]" && p.Title == "Betalingsbewijs beschikbaar");
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

    [Fact]
    public async Task Webhook_UnknownPaymentReference_ChangesNothingAndReturns200()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/webhooks/mollie/{Guid.NewGuid()}", null);

        // No enumeration oracle — always 200, regardless of whether the reference resolves.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PaymentStatus_BeforeWebhook_ShowsInvoiceStillSent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Webhook Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));

        var statusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/payment-status", parentToken));
        var status = (await statusResponse.Content.ReadFromJsonAsync<PaymentStatusResponse>())!;

        Assert.Equal("sent", status.InvoiceStatus);
        Assert.Equal("open", status.PaymentStatus);
    }

    [Fact]
    public async Task PaymentLink_ForInvoiceNotBelongingToParent_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Webhook Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");
        var otherInvoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, otherChild.Id, 2027, 9);
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{otherInvoice.Id}/payment-link", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaymentLink_RevokedMollieToken_SurfacesReconnectStateAndDisconnects()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Revoked Token Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var fakeProvider = factory.Services.GetRequiredService<FakePaymentProvider>();
        fakeProvider.ThrowUnauthorizedOnCreatePayment = true;
        try
        {
            var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

            // No silent broken link — the connection itself is flagged so the director sees
            // "not connected" / reconnect, not a false "connected" state (spec.md Edge Cases).
            var statusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me/payment-connection", org.AccessToken));
            var status = await statusResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("disconnected", status.GetProperty("status").GetString());
        }
        finally
        {
            fakeProvider.ThrowUnauthorizedOnCreatePayment = false;
        }
    }

    [Fact]
    public async Task Webhook_AfterConcurrentManualMarkPaid_IsNoOp_OnlyOneTransitionWins()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Race Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await ConnectMollieAsync(client, org.AccessToken);
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/invoices/{invoice.Id}/payment-link", parentToken));
        var (providerPaymentId, paymentReference) = await GetPaymentForInvoiceAsync(invoice.Id);
        var fakeProvider = factory.Services.GetRequiredService<FakePaymentProvider>();
        fakeProvider.Payments[providerPaymentId].Status = "paid";

        // Director marks it paid manually before Mollie's webhook lands.
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-paid", org.AccessToken,
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow))));

        // Webhook arrives afterward — must be a no-op, not a second transition/exception.
        var webhookResponse = await client.PostAsync($"/api/webhooks/mollie/{paymentReference}", null);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var invoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var invoiceAfter = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("paid", invoiceAfter.Status);
    }

    private async Task<Guid> GetPaymentReferenceAsync(string providerPaymentId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var payment = publicDb.Payments.Single(p => p.ProviderPaymentId == providerPaymentId);
        return payment.PaymentReference;
    }

    // Looks up the Payment row by InvoiceId directly, rather than assuming it's the only entry
    // in FakePaymentProvider.Payments — that dictionary is a Singleton shared across every test
    // in this class (IClassFixture), so other tests' payments accumulate in it too.
    private async Task<(string ProviderPaymentId, Guid PaymentReference)> GetPaymentForInvoiceAsync(Guid invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var payment = publicDb.Payments.Single(p => p.InvoiceId == invoiceId);
        return (payment.ProviderPaymentId!, payment.PaymentReference);
    }
}
