using System.Net;
using System.Net.Http;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-004.
// Reuses an existing Open Payment for the invoice rather than creating a second one
// (research.md R6, 2026-07-16 clarification). TenantUserId/contact-linkage check mirrors
// GenerateParentInvoicePdfQuery's existing precedent (014) — a parent may only act on their own
// child's invoice.
public record CreatePaymentLinkCommand(Guid TenantUserId, Guid InvoiceId) : IRequest<CreatePaymentLinkResult>;

// ProviderConnectionInvalid: the stored OAuth token was rejected by Mollie (revoked/expired,
// spec.md Edge Cases — "reconnect" case) — distinct from ProviderNotConnected (no connection
// row at all) only in the failure copy shown; both drive the same parent-facing not-available
// state (contracts/014a-invoice-payments-plus/payments-api.md).
public enum CreatePaymentLinkFailure { InvoiceNotFound, InvoiceNotSent, ProviderNotConnected, ProviderConnectionInvalid }

public class CreatePaymentLinkResult
{
    public string? CheckoutUrl { get; private init; }
    public CreatePaymentLinkFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static CreatePaymentLinkResult Success(string checkoutUrl) => new() { CheckoutUrl = checkoutUrl };
    public static CreatePaymentLinkResult Fail(CreatePaymentLinkFailure failure) => new() { Failure = failure };
}

public class CreatePaymentLinkCommandValidator : AbstractValidator<CreatePaymentLinkCommand> { }

public class CreatePaymentLinkCommandHandler(
    ITenantDbContext db,
    IPublicDbContext publicDb,
    ICurrentTenantService currentTenant,
    ICurrentParentContactResolver contactResolver,
    IPaymentProvider paymentProvider,
    IPaymentTokenProtector tokenProtector,
    IConfiguration configuration)
    : IRequestHandler<CreatePaymentLinkCommand, CreatePaymentLinkResult>
{
    public async Task<CreatePaymentLinkResult> Handle(CreatePaymentLinkCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return CreatePaymentLinkResult.Fail(CreatePaymentLinkFailure.InvoiceNotFound);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return CreatePaymentLinkResult.Fail(CreatePaymentLinkFailure.InvoiceNotFound);

        var isLinked = await db.ChildContacts.AnyAsync(cc => cc.ChildId == invoice.ChildId && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return CreatePaymentLinkResult.Fail(CreatePaymentLinkFailure.InvoiceNotFound);

        if (invoice.Status != InvoiceStatus.Sent)
            return CreatePaymentLinkResult.Fail(CreatePaymentLinkFailure.InvoiceNotSent);

        var connection = await publicDb.PaymentProviderConnections
            .FirstOrDefaultAsync(c => c.TenantId == currentTenant.TenantId && c.Status == PaymentConnectionStatus.Connected, cancellationToken);
        if (connection is null)
            return CreatePaymentLinkResult.Fail(CreatePaymentLinkFailure.ProviderNotConnected);

        // Feature 030 (US3, spec.md FR-009a/Clarifications) — "one payment action covers the
        // whole bundled group": paying any one sibling invoice online must charge the group's
        // combined total, matching what the parent app already displays as the family's
        // combinedTotal, and matching MarkInvoicePaidCommand's manual-path cascade amount.
        var amountCents = invoice.FamilyGroupId is null
            ? invoice.TotalCents
            : await db.Invoices
                .Where(i => i.FamilyGroupId == invoice.FamilyGroupId && i.Status == InvoiceStatus.Sent)
                .SumAsync(i => i.TotalCents, cancellationToken);

        // research.md R6 — reuse an existing active attempt rather than creating a second one.
        var existing = await publicDb.Payments
            .Where(p => p.TenantId == currentTenant.TenantId && p.InvoiceId == request.InvoiceId && p.Status == PaymentStatus.Open)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var credentials = new PaymentProviderCredentials(tokenProtector.Unprotect(connection.EncryptedAccessToken));

        try
        {
            if (existing is not null && existing.ProviderPaymentId is not null)
            {
                // Re-check live status rather than trusting our own stored Open flag — Mollie's
                // payment may have expired/failed/been cancelled since we last touched it.
                var status = await paymentProvider.GetPaymentStatusAsync(credentials, existing.ProviderPaymentId, cancellationToken);
                if (status.Status == "open" && status.CheckoutUrl is not null)
                    return CreatePaymentLinkResult.Success(status.CheckoutUrl);

                // No longer active on Mollie's side — mark it terminal locally so a future lookup
                // doesn't keep re-checking a dead payment, then fall through to create a new one.
                existing.Status = ParsePaymentStatus(status.Status);
                existing.UpdatedAt = DateTime.UtcNow;
                await publicDb.SaveChangesAsync(cancellationToken);
            }

            var payment = new Payment
            {
                TenantId = currentTenant.TenantId,
                InvoiceId = invoice.Id,
                AmountCents = amountCents,
                Status = PaymentStatus.Open,
            };
            publicDb.Payments.Add(payment);
            await publicDb.SaveChangesAsync(cancellationToken);

            var result = await paymentProvider.CreatePaymentAsync(
                credentials, payment.PaymentReference, payment.AmountCents,
                $"Invoice {invoice.OgmReference}", BuildWebhookUrl(payment.PaymentReference), cancellationToken);

            payment.ProviderPaymentId = result.ProviderPaymentId;
            payment.UpdatedAt = DateTime.UtcNow;
            await publicDb.SaveChangesAsync(cancellationToken);

            return CreatePaymentLinkResult.Success(result.CheckoutUrl);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // spec.md Edge Cases — a revoked/expired OAuth token must surface a "reconnect"
            // state to the director, not a silent broken link to the parent or an unhandled
            // 500. Any other transport/transient failure is left to the global exception
            // handler (constitution VI), which already returns a generic, safe error.
            connection.Status = PaymentConnectionStatus.Disconnected;
            connection.DisconnectedAt = DateTime.UtcNow;
            await publicDb.SaveChangesAsync(cancellationToken);
            return CreatePaymentLinkResult.Fail(CreatePaymentLinkFailure.ProviderConnectionInvalid);
        }
    }

    private string BuildWebhookUrl(Guid paymentReference)
    {
        var baseUrl = configuration["App:ApiBaseUrl"] ?? throw new InvalidOperationException("App:ApiBaseUrl is not configured.");
        return $"{baseUrl.TrimEnd('/')}/api/webhooks/mollie/{paymentReference}";
    }

    private static PaymentStatus ParsePaymentStatus(string mollieStatus) => mollieStatus switch
    {
        "paid" => PaymentStatus.Paid,
        "failed" => PaymentStatus.Failed,
        "canceled" or "cancelled" => PaymentStatus.Cancelled,
        "expired" => PaymentStatus.Expired,
        _ => PaymentStatus.Open,
    };
}
