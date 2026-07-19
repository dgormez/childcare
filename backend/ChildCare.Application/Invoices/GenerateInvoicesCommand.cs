using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-001/FR-002/FR-003/FR-014. Bulk-generates a draft invoice per child
// with a contract active at any point during the location/month; re-running for the same
// location/month recomputes any still-draft invoice in place rather than duplicating it
// (US1/AC5) — a Sent or Paid invoice for that same key is left untouched (only
// RegenerateInvoiceCommand touches a Sent one; a Paid one is immutable, FR-012).
public record GenerateInvoicesCommand(Guid LocationId, int Year, int Month) : IRequest<GenerateInvoicesResult>;

public record GenerateInvoicesResult(bool LocationFound, IReadOnlyList<InvoiceResponse> Invoices);

public class GenerateInvoicesCommandValidator : AbstractValidator<GenerateInvoicesCommand>
{
    public GenerateInvoicesCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public class GenerateInvoicesCommandHandler(ITenantDbContext db, BillableDayCalculator billableDayCalculator)
    : IRequestHandler<GenerateInvoicesCommand, GenerateInvoicesResult>
{
    // Feature 030 — the well-known label identifying a system-computed sibling discount line,
    // so a regenerate can strip and recompute it fresh each run without duplicating it or
    // touching a director's own manually-added extra charges (which never use this label).
    private const string SiblingDiscountLabel = "invoices.lineItems.siblingDiscount";

    public async Task<GenerateInvoicesResult> Handle(GenerateInvoicesCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return new GenerateInvoicesResult(false, []);

        var periodMonth = new DateOnly(request.Year, request.Month, 1);
        var monthStart = periodMonth;
        var monthEnd = periodMonth.AddMonths(1).AddDays(-1);

        // FR-001/Edge Cases: a contract that was never Active (still Draft) never yields an
        // invoice, even if its dates would otherwise overlap the month.
        var contracts = await db.Contracts
            .Where(c => c.LocationId == request.LocationId
                && c.Status != ContractStatus.Draft
                && c.StartDate <= monthEnd
                && (c.EndDate == null || c.EndDate >= monthStart))
            .ToListAsync(cancellationToken);

        var existingInvoices = await db.Invoices
            .Where(i => i.LocationId == request.LocationId && i.PeriodMonth == periodMonth
                && contracts.Select(c => c.Id).Contains(i.ContractId))
            .ToListAsync(cancellationToken);

        // Recomputed = still-Draft this run (eligible for the sibling discount/bundling pass
        // below); a Sent/Paid invoice is carried through untouched (see class doc comment).
        var invoiceEntries = new List<(Contract Contract, Invoice Invoice, bool Recomputed)>();

        foreach (var contract in contracts)
        {
            var range = BillableDayCalculator.EffectiveRange(contract, request.Year, request.Month);
            if (range is null)
                continue;

            var existing = existingInvoices.FirstOrDefault(i => i.ChildId == contract.ChildId && i.ContractId == contract.Id);
            if (existing is not null && existing.Status != InvoiceStatus.Draft)
            {
                invoiceEntries.Add((contract, existing, false));
                continue;
            }

            var billable = await billableDayCalculator.ComputeAsync(
                contract.ChildId, request.LocationId, range.Value.Start, range.Value.End, cancellationToken);

            // The prior sibling-discount line (if any) is always stripped here and recomputed
            // fresh in the pass below — a director's own manually-added extra charges (never
            // under this label) are preserved untouched.
            var priorExtraCharges = existing is not null
                ? InvoiceLineItems.FromJson(existing.LineItems).ExtraCharges.Where(c => c.Label != SiblingDiscountLabel).ToList()
                : [];

            var lineItems = new InvoiceLineItems(
                billable.PresentDays,
                billable.UnjustifiedAbsentDays,
                contract.DailyRateCents,
                billable.ClosureDaysExcluded,
                billable.DaysMin5u,
                billable.DaysMin11u,
                priorExtraCharges);

            var invoice = existing ?? new Invoice
            {
                ChildId = contract.ChildId,
                ContractId = contract.Id,
                LocationId = request.LocationId,
                PeriodMonth = periodMonth,
            };

            invoice.SubtotalCents = lineItems.SubtotalCents;
            invoice.TotalCents = lineItems.TotalCents;
            invoice.LineItems = lineItems.ToJson();
            invoice.UpdatedAt = DateTime.UtcNow;

            if (existing is null)
            {
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync(cancellationToken); // assigns SequenceNumber (identity column)
                invoice.OgmReference = OgmReferenceGenerator.Generate(invoice.SequenceNumber);
            }

            await db.SaveChangesAsync(cancellationToken);
            invoiceEntries.Add((contract, invoice, true));
        }

        await ApplySiblingDiscountAndBundlingAsync(location, contracts, invoiceEntries, cancellationToken);

        var childrenById = await db.Children
            .Where(c => invoiceEntries.Select(e => e.Invoice.ChildId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var responses = invoiceEntries
            .Select(e => InvoiceMapper.ToResponse(e.Invoice, $"{childrenById[e.Invoice.ChildId].FirstName} {childrenById[e.Invoice.ChildId].LastName}", location.Name))
            .ToList();

        return new GenerateInvoicesResult(true, responses);
    }

    // Feature 030 (US2/US3) — sibling discount and family invoice bundling are both computed
    // once, at generation time (research.md R2), grouped by each child's primary ChildContact
    // (research.md R3/R4) so both features share one resolution of "who's family". Only
    // still-Draft invoices (Recomputed) are mutated — a Sent/Paid invoice's discount/grouping is
    // frozen from whenever it was generated (spec.md Clarifications).
    private async Task ApplySiblingDiscountAndBundlingAsync(
        Location location,
        List<Contract> contracts,
        List<(Contract Contract, Invoice Invoice, bool Recomputed)> invoiceEntries,
        CancellationToken cancellationToken)
    {
        if (invoiceEntries.Count == 0 || (location.SiblingDiscountPct <= 0 && !location.FamilyInvoiceBundlingEnabled))
            return;

        var childIds = invoiceEntries.Select(e => e.Invoice.ChildId).Distinct().ToList();
        var primaryContactByChildId = await db.ChildContacts
            .Where(cc => cc.IsPrimary && childIds.Contains(cc.ChildId))
            .ToDictionaryAsync(cc => cc.ChildId, cc => cc.ContactId, cancellationToken);

        // Feature 030 (spec.md Assumptions) — tie-broken by the earliest-*created* contract
        // record when two siblings' contracts share the exact same start date (e.g. twins
        // signed on one contract date), so the "full price" pick stays fully deterministic.
        var earliestContractByChildId = contracts
            .Where(c => childIds.Contains(c.ChildId))
            .GroupBy(c => c.ChildId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.StartDate).ThenBy(c => c.CreatedAt).First());

        var groups = invoiceEntries
            .Where(e => primaryContactByChildId.ContainsKey(e.Invoice.ChildId))
            .GroupBy(e => primaryContactByChildId[e.Invoice.ChildId])
            .Where(g => g.Select(e => e.Invoice.ChildId).Distinct().Count() >= 2);

        var anyChanges = false;

        foreach (var group in groups)
        {
            // research.md R3: the earliest-enrolled sibling (by contract start date) at this
            // location is full price; every other sibling in the group is discounted.
            var fullPriceChildId = group.Select(e => e.Invoice.ChildId).Distinct()
                .OrderBy(childId => earliestContractByChildId[childId].StartDate)
                .ThenBy(childId => earliestContractByChildId[childId].CreatedAt)
                .First();

            if (location.SiblingDiscountPct > 0)
            {
                foreach (var entry in group.Where(e => e.Recomputed && e.Invoice.ChildId != fullPriceChildId))
                {
                    var lineItems = InvoiceLineItems.FromJson(entry.Invoice.LineItems);
                    var discountCents = -(int)Math.Round(lineItems.SubtotalCents * location.SiblingDiscountPct / 100m, MidpointRounding.AwayFromZero);
                    var withDiscount = lineItems with
                    {
                        ExtraCharges = lineItems.ExtraCharges.Append(new InvoiceExtraCharge(SiblingDiscountLabel, discountCents)).ToList(),
                    };
                    entry.Invoice.LineItems = withDiscount.ToJson();
                    entry.Invoice.TotalCents = withDiscount.TotalCents;
                    entry.Invoice.UpdatedAt = DateTime.UtcNow;
                    anyChanges = true;
                }
            }

            if (location.FamilyInvoiceBundlingEnabled)
            {
                // research.md R4: reuse a group's existing FamilyGroupId (from a prior run)
                // rather than minting a new one each regenerate, so the group identity is
                // stable across regenerates of the same still-open group.
                var familyGroupId = group.Select(e => e.Invoice.FamilyGroupId).FirstOrDefault(id => id is not null) ?? Guid.NewGuid();
                foreach (var entry in group.Where(e => e.Recomputed && e.Invoice.FamilyGroupId != familyGroupId))
                {
                    entry.Invoice.FamilyGroupId = familyGroupId;
                    entry.Invoice.UpdatedAt = DateTime.UtcNow;
                    anyChanges = true;
                }
            }
        }

        if (anyChanges)
            await db.SaveChangesAsync(cancellationToken);
    }
}
