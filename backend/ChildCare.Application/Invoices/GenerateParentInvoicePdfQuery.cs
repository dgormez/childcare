using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md Security considerations: an invoice that doesn't exist, doesn't belong
// to one of the requesting parent's children, or is still Draft all produce the identical
// "not found" outcome (Found = false) — deliberately indistinguishable, so a caller can't
// enumerate invoice existence/ownership by probing (matches this codebase's established
// invitation-enumeration precedent, research.md R5 in feature 001).
public record GenerateParentInvoicePdfQuery(Guid TenantUserId, Guid InvoiceId, string? Locale) : IRequest<GenerateInvoicePdfResult>;

public class GenerateParentInvoicePdfQueryHandler(
    ITenantDbContext db, ICurrentParentContactResolver contactResolver, IMediator mediator)
    : IRequestHandler<GenerateParentInvoicePdfQuery, GenerateInvoicePdfResult>
{
    public async Task<GenerateInvoicePdfResult> Handle(GenerateParentInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return new GenerateInvoicePdfResult(false, []);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.Status == InvoiceStatus.Draft)
            return new GenerateInvoicePdfResult(false, []);

        var isLinked = await db.ChildContacts.AnyAsync(cc => cc.ChildId == invoice.ChildId && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return new GenerateInvoicePdfResult(false, []);

        return await mediator.Send(new GenerateInvoicePdfQuery(request.InvoiceId, request.Locale), cancellationToken);
    }
}
