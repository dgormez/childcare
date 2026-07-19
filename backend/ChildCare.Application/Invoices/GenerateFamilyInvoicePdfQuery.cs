using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 030 (US3) — contracts/family-siblings-api.md, research.md R5. Loads every Invoice
// sharing the given FamilyGroupId and renders one combined PDF — mirrors GenerateInvoicePdfQuery,
// extended to loop children instead of assuming one.
public record GenerateFamilyInvoicePdfQuery(Guid FamilyGroupId, string? Locale) : IRequest<GenerateInvoicePdfResult>;

public class GenerateFamilyInvoicePdfQueryHandler(
    ITenantDbContext db, IPublicDbContext publicDb, ICurrentTenantService currentTenant, IFamilyInvoicePdfGenerator pdfGenerator)
    : IRequestHandler<GenerateFamilyInvoicePdfQuery, GenerateInvoicePdfResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateInvoicePdfResult> Handle(GenerateFamilyInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoices = await db.Invoices
            .Where(i => i.FamilyGroupId == request.FamilyGroupId)
            .ToListAsync(cancellationToken);
        if (invoices.Count == 0)
            return new GenerateInvoicePdfResult(false, []);

        var first = invoices[0];
        var location = await db.Locations.FirstAsync(l => l.Id == first.LocationId, cancellationToken);
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);

        var childIds = invoices.Select(i => i.ChildId).ToList();
        var childrenById = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        // Every invoice in the group shares the same primary contact by construction
        // (research.md R3/R4) — any one child's primary contact represents the whole family.
        var primaryContact = await db.ChildContacts
            .Where(cc => cc.ChildId == first.ChildId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .FirstOrDefaultAsync(cancellationToken);

        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var sections = invoices.Select(invoice =>
        {
            var lineItems = InvoiceLineItems.FromJson(invoice.LineItems);
            var child = childrenById[invoice.ChildId];
            return new FamilyInvoicePdfChildSection(
                $"{child.FirstName} {child.LastName}",
                lineItems.PresentDays,
                lineItems.UnjustifiedAbsentDays,
                lineItems.DailyRateCents,
                lineItems.ExtraCharges.Select(c => new InvoicePdfExtraCharge(c.Label, c.AmountCents)).ToList(),
                invoice.TotalCents,
                invoice.OgmReference);
        }).ToList();

        var model = new FamilyInvoicePdfModel(
            location.Name,
            location.Address,
            tenant.KboNumber,
            location.Erkenningsnummer,
            location.BankAccountNumber,
            primaryContact is null ? string.Empty : $"{primaryContact.FirstName} {primaryContact.LastName}",
            first.PeriodMonth.Year,
            first.PeriodMonth.Month,
            sections,
            invoices.Sum(i => i.TotalCents),
            first.DueDate,
            locale);

        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateInvoicePdfResult(true, bytes);
    }
}

// Feature 030 (US3) — parent-facing wrapper (contracts/family-siblings-api.md). Same
// indistinguishable-not-found authorization pattern as GenerateParentInvoicePdfQuery: authorized
// if the caller is linked to any child among the grouped invoices.
public record GenerateParentFamilyInvoicePdfQuery(Guid TenantUserId, Guid FamilyGroupId, string? Locale) : IRequest<GenerateInvoicePdfResult>;

public class GenerateParentFamilyInvoicePdfQueryHandler(
    ITenantDbContext db, ICurrentParentContactResolver contactResolver, IMediator mediator)
    : IRequestHandler<GenerateParentFamilyInvoicePdfQuery, GenerateInvoicePdfResult>
{
    public async Task<GenerateInvoicePdfResult> Handle(GenerateParentFamilyInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return new GenerateInvoicePdfResult(false, []);

        var groupChildIds = await db.Invoices
            .Where(i => i.FamilyGroupId == request.FamilyGroupId)
            .Select(i => i.ChildId)
            .ToListAsync(cancellationToken);
        if (groupChildIds.Count == 0)
            return new GenerateInvoicePdfResult(false, []);

        var isLinked = await db.ChildContacts.AnyAsync(cc => groupChildIds.Contains(cc.ChildId) && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return new GenerateInvoicePdfResult(false, []);

        return await mediator.Send(new GenerateFamilyInvoicePdfQuery(request.FamilyGroupId, request.Locale), cancellationToken);
    }
}
