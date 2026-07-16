using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-010. Polled
// by parent-mobile after returning from Mollie's hosted page, to resolve the "confirming
// payment" state without waiting on any other channel for the webhook to land. Contact-linkage
// check mirrors GenerateParentInvoicePdfQuery's existing precedent (014).
public record GetPaymentStatusQuery(Guid TenantUserId, Guid InvoiceId) : IRequest<GetPaymentStatusResult>;

public record GetPaymentStatusResult(bool Found, string? InvoiceStatus, string? PaymentStatus);

public class GetPaymentStatusQueryHandler(
    ITenantDbContext db, IPublicDbContext publicDb, ICurrentTenantService currentTenant, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<GetPaymentStatusQuery, GetPaymentStatusResult>
{
    public async Task<GetPaymentStatusResult> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return new GetPaymentStatusResult(false, null, null);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new GetPaymentStatusResult(false, null, null);

        var isLinked = await db.ChildContacts.AnyAsync(cc => cc.ChildId == invoice.ChildId && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return new GetPaymentStatusResult(false, null, null);

        var payment = await publicDb.Payments
            .Where(p => p.TenantId == currentTenant.TenantId && p.InvoiceId == request.InvoiceId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new GetPaymentStatusResult(
            true,
            invoice.Status.ToString().ToLowerInvariant(),
            payment?.Status.ToString().ToLowerInvariant());
    }
}
