using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.SepaBatches;

// Feature 026 — contracts/sepa-direct-debit-api.md, spec.md FR-001/FR-004.
public record GetSepaBatchEligibilityQuery(Guid LocationId, DateOnly Month) : IRequest<SepaBatchEligibilityResponse>;

public class GetSepaBatchEligibilityQueryHandler(ITenantDbContext db, IPublicDbContext publicDb, ICurrentTenantService currentTenant)
    : IRequestHandler<GetSepaBatchEligibilityQuery, SepaBatchEligibilityResponse>
{
    public async Task<SepaBatchEligibilityResponse> Handle(GetSepaBatchEligibilityQuery request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstAsync(l => l.Id == request.LocationId, cancellationToken);
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);
        var creditorConfigured = !string.IsNullOrWhiteSpace(tenant.SepaCreditorIdentifier) && !string.IsNullOrWhiteSpace(location.BankAccountNumber);

        var periodMonth = new DateOnly(request.Month.Year, request.Month.Month, 1);
        var candidates = await db.Invoices
            .Where(i => i.LocationId == request.LocationId && i.PeriodMonth == periodMonth && i.Status == InvoiceStatus.Sent)
            .Join(db.Contracts, i => i.ContractId, c => c.Id, (i, c) => new { Invoice = i, Contract = c })
            .Join(db.Children, x => x.Invoice.ChildId, ch => ch.Id, (x, ch) => new { x.Invoice, x.Contract, Child = ch })
            .ToListAsync(cancellationToken);

        var eligible = new List<SepaBatchEligibleInvoiceResponse>();
        var excluded = new List<SepaBatchExcludedInvoiceResponse>();

        foreach (var row in candidates)
        {
            var childName = $"{row.Child.FirstName} {row.Child.LastName}";

            // Priority order (data-model.md's Eligibility rule): no mandate, then revoked
            // mandate, then non-positive amount.
            if (row.Contract.SepaAuthorisedAt is null)
            {
                excluded.Add(new SepaBatchExcludedInvoiceResponse(row.Invoice.Id, childName, row.Invoice.TotalCents, "NoMandate"));
                continue;
            }
            if (row.Contract.SepaRevokedAt is not null)
            {
                excluded.Add(new SepaBatchExcludedInvoiceResponse(row.Invoice.Id, childName, row.Invoice.TotalCents, "MandateRevoked"));
                continue;
            }
            if (row.Invoice.TotalCents <= 0)
            {
                excluded.Add(new SepaBatchExcludedInvoiceResponse(row.Invoice.Id, childName, row.Invoice.TotalCents, "NonPositiveAmount"));
                continue;
            }

            var debtorName = await ResolveDebtorNameAsync(row.Invoice.ChildId, cancellationToken);
            eligible.Add(new SepaBatchEligibleInvoiceResponse(row.Invoice.Id, childName, row.Invoice.TotalCents, debtorName));
        }

        return new SepaBatchEligibilityResponse(creditorConfigured, eligible, excluded);
    }

    // Mirrors GenerateInvoicePdfQuery's exact primary-contact resolution (research.md R6) — the
    // same name already printed as a family's invoice addressee.
    internal static async Task<string> ResolveDebtorNameAsync(ITenantDbContext db, Guid childId, CancellationToken cancellationToken)
    {
        var primaryContact = await db.ChildContacts
            .Where(cc => cc.ChildId == childId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .FirstOrDefaultAsync(cancellationToken);
        return primaryContact is null ? string.Empty : $"{primaryContact.FirstName} {primaryContact.LastName}";
    }

    private Task<string> ResolveDebtorNameAsync(Guid childId, CancellationToken cancellationToken) =>
        ResolveDebtorNameAsync(db, childId, cancellationToken);
}
