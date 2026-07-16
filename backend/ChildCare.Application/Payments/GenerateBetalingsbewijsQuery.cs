using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-017. A
// not-yet-paid invoice, one that doesn't exist, or one that doesn't belong to the requesting
// parent's children are all indistinguishable "not found" outcomes (mirrors
// GenerateParentInvoicePdfQuery's existing enumeration-resistance precedent, 014).
public record GenerateBetalingsbewijsQuery(Guid TenantUserId, Guid InvoiceId, string? Locale) : IRequest<GenerateBetalingsbewijsResult>;

public record GenerateBetalingsbewijsResult(bool Found, byte[] Bytes);

public class GenerateBetalingsbewijsQueryHandler(
    ITenantDbContext db, ICurrentParentContactResolver contactResolver, IBetalingsbewijsGenerator pdfGenerator)
    : IRequestHandler<GenerateBetalingsbewijsQuery, GenerateBetalingsbewijsResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateBetalingsbewijsResult> Handle(GenerateBetalingsbewijsQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return new GenerateBetalingsbewijsResult(false, []);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.Status != InvoiceStatus.Paid || invoice.PaidAt is null)
            return new GenerateBetalingsbewijsResult(false, []);

        var isLinked = await db.ChildContacts.AnyAsync(cc => cc.ChildId == invoice.ChildId && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return new GenerateBetalingsbewijsResult(false, []);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);

        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var model = new BetalingsbewijsModel(
            location.Name,
            location.Address,
            $"{contact.FirstName} {contact.LastName}",
            $"{child.FirstName} {child.LastName}",
            invoice.OgmReference,
            invoice.TotalCents,
            invoice.PaidAt.Value,
            locale);

        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateBetalingsbewijsResult(true, bytes);
    }
}
