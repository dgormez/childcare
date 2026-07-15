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

        var responses = new List<InvoiceResponse>();

        foreach (var contract in contracts)
        {
            var range = BillableDayCalculator.EffectiveRange(contract, request.Year, request.Month);
            if (range is null)
                continue;

            var existing = existingInvoices.FirstOrDefault(i => i.ChildId == contract.ChildId && i.ContractId == contract.Id);
            if (existing is not null && existing.Status != InvoiceStatus.Draft)
            {
                // Already sent/paid for this key — leave it alone (see class doc comment).
                var untouchedChild = await db.Children.FirstAsync(c => c.Id == existing.ChildId, cancellationToken);
                responses.Add(InvoiceMapper.ToResponse(existing, $"{untouchedChild.FirstName} {untouchedChild.LastName}", location.Name));
                continue;
            }

            var billable = await billableDayCalculator.ComputeAsync(
                contract.ChildId, request.LocationId, range.Value.Start, range.Value.End, cancellationToken);

            var lineItems = new InvoiceLineItems(
                billable.PresentDays,
                billable.UnjustifiedAbsentDays,
                contract.DailyRateCents,
                billable.ClosureDaysExcluded,
                billable.DaysMin5u,
                billable.DaysMin11u,
                existing is not null ? InvoiceLineItems.FromJson(existing.LineItems).ExtraCharges : []);

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

            var child = await db.Children.FirstAsync(c => c.Id == contract.ChildId, cancellationToken);
            responses.Add(InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name));
        }

        return new GenerateInvoicesResult(true, responses);
    }
}
