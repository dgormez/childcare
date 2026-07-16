using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-006/FR-007/
// FR-008/FR-009. Deliberately takes ONLY the opaque PaymentReference — never any tenant/invoice
// identifier the caller might supply — so there is structurally no field to "trust" from the
// webhook payload (research.md R2). The real payment status is always re-fetched from the
// provider directly (FR-007), never taken from whatever the webhook call claims.
public record ProcessPaymentWebhookCommand(Guid PaymentReference) : IRequest<ProcessPaymentWebhookResult>;

public record ProcessPaymentWebhookResult(bool Resolved);

public class ProcessPaymentWebhookCommandValidator : AbstractValidator<ProcessPaymentWebhookCommand> { }

public class ProcessPaymentWebhookCommandHandler(
    IPublicDbContext publicDb,
    ITenantDbContextResolver tenantResolver,
    IPaymentProvider paymentProvider,
    IPaymentTokenProtector tokenProtector,
    PaymentReceiptNotificationService receiptNotificationService)
    : IRequestHandler<ProcessPaymentWebhookCommand, ProcessPaymentWebhookResult>
{
    public async Task<ProcessPaymentWebhookResult> Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        // Not found — never reveal why (spec.md Edge Cases: no tenant-enumeration oracle).
        var payment = await publicDb.Payments.FirstOrDefaultAsync(p => p.PaymentReference == request.PaymentReference, cancellationToken);
        if (payment is null || payment.ProviderPaymentId is null)
            return new ProcessPaymentWebhookResult(false);

        // Already terminal — idempotent no-op regardless of how many more times this fires
        // (spec.md FR-009).
        if (payment.Status is PaymentStatus.Paid or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired)
            return new ProcessPaymentWebhookResult(true);

        var connection = await publicDb.PaymentProviderConnections
            .FirstOrDefaultAsync(c => c.TenantId == payment.TenantId, cancellationToken);
        if (connection is null)
            return new ProcessPaymentWebhookResult(false);

        var credentials = new PaymentProviderCredentials(tokenProtector.Unprotect(connection.EncryptedAccessToken));
        var status = await paymentProvider.GetPaymentStatusAsync(credentials, payment.ProviderPaymentId, cancellationToken);

        var newStatus = ParsePaymentStatus(status.Status);
        payment.Status = newStatus;
        payment.FeeCents = status.FeeCents;
        payment.UpdatedAt = DateTime.UtcNow;

        if (newStatus == PaymentStatus.Paid)
        {
            var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == payment.TenantId, cancellationToken);
            var tenantDb = tenantResolver.ForSchema(tenant.SchemaName);

            var invoice = await tenantDb.Invoices.FirstOrDefaultAsync(i => i.Id == payment.InvoiceId, cancellationToken);
            // Same one-way transition 014 already enforces (spec.md FR-008) — a concurrent
            // manual mark-paid may have already won the race (spec.md Edge Cases).
            if (invoice is not null && invoice.Status == InvoiceStatus.Sent)
            {
                invoice.Status = InvoiceStatus.Paid;
                invoice.PaidAt = DateTime.UtcNow;
                invoice.UpdatedAt = DateTime.UtcNow;
                await tenantDb.SaveChangesAsync(cancellationToken);

                await receiptNotificationService.NotifyAsync(tenantDb, invoice, cancellationToken);
            }
        }

        await publicDb.SaveChangesAsync(cancellationToken);
        return new ProcessPaymentWebhookResult(true);
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
