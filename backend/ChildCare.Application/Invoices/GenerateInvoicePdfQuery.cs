using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — mirrors GenerateContractPdfQuery: rendered on-demand from the invoice's current
// stored state, never persisted to storage (research.md R1).
public record GenerateInvoicePdfQuery(Guid Id, string? Locale) : IRequest<GenerateInvoicePdfResult>;

public record GenerateInvoicePdfResult(bool Found, byte[] Bytes);

public class GenerateInvoicePdfQueryHandler(ITenantDbContext db, IPublicDbContext publicDb, ICurrentTenantService currentTenant, IInvoicePdfGenerator pdfGenerator)
    : IRequestHandler<GenerateInvoicePdfQuery, GenerateInvoicePdfResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateInvoicePdfResult> Handle(GenerateInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (invoice is null)
            return new GenerateInvoicePdfResult(false, []);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);

        var primaryContact = await db.ChildContacts
            .Where(cc => cc.ChildId == invoice.ChildId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .FirstOrDefaultAsync(cancellationToken);

        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";
        var lineItems = InvoiceLineItems.FromJson(invoice.LineItems);

        var model = new InvoicePdfModel(
            location.Name,
            location.Address,
            tenant.KboNumber,
            location.Erkenningsnummer,
            location.BankAccountNumber,
            primaryContact is null ? string.Empty : $"{primaryContact.FirstName} {primaryContact.LastName}",
            $"{child.FirstName} {child.LastName}",
            invoice.PeriodMonth.Year,
            invoice.PeriodMonth.Month,
            lineItems.PresentDays,
            lineItems.UnjustifiedAbsentDays,
            lineItems.DailyRateCents,
            lineItems.ExtraCharges.Select(c => new InvoicePdfExtraCharge(c.Label, c.AmountCents)).ToList(),
            invoice.TotalCents,
            invoice.DueDate,
            invoice.OgmReference,
            locale);

        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateInvoicePdfResult(true, bytes);
    }
}
