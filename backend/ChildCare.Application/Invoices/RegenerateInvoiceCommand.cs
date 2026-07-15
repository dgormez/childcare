using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-011/FR-012/US4. Valid on Draft or Sent; rejected on Paid with no
// write attempted (FR-012's "every field including UpdatedAt untouched" guarantee — the check
// happens before anything is read for update). Preserves OgmReference/SentAt/DueDate always;
// re-notifies the parent only when the invoice was already Sent.
public record RegenerateInvoiceCommand(Guid InvoiceId) : IRequest<RegenerateInvoiceResult>;

public enum RegenerateInvoiceFailure { NotFound, PaidImmutable }

public class RegenerateInvoiceResult
{
    public InvoiceResponse? Response { get; private init; }
    public RegenerateInvoiceFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static RegenerateInvoiceResult Success(InvoiceResponse response) => new() { Response = response };
    public static RegenerateInvoiceResult Fail(RegenerateInvoiceFailure failure) => new() { Failure = failure };
}

public class RegenerateInvoiceCommandHandler(
    ITenantDbContext db, BillableDayCalculator billableDayCalculator, InvoiceNotificationService notifications)
    : IRequestHandler<RegenerateInvoiceCommand, RegenerateInvoiceResult>
{
    public async Task<RegenerateInvoiceResult> Handle(RegenerateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return RegenerateInvoiceResult.Fail(RegenerateInvoiceFailure.NotFound);
        if (invoice.Status == InvoiceStatus.Paid)
            return RegenerateInvoiceResult.Fail(RegenerateInvoiceFailure.PaidImmutable);

        var contract = await db.Contracts.FirstAsync(c => c.Id == invoice.ContractId, cancellationToken);
        var range = BillableDayCalculator.EffectiveRange(contract, invoice.PeriodMonth.Year, invoice.PeriodMonth.Month);
        var billable = range is null
            ? new BillableDayResult(0, 0, 0, 0, 0)
            : await billableDayCalculator.ComputeAsync(invoice.ChildId, invoice.LocationId, range.Value.Start, range.Value.End, cancellationToken);

        var existingExtraCharges = InvoiceLineItems.FromJson(invoice.LineItems).ExtraCharges;
        var lineItems = new InvoiceLineItems(
            billable.PresentDays, billable.UnjustifiedAbsentDays, contract.DailyRateCents,
            billable.ClosureDaysExcluded, billable.DaysMin5u, billable.DaysMin11u, existingExtraCharges);

        invoice.LineItems = lineItems.ToJson();
        invoice.SubtotalCents = lineItems.SubtotalCents;
        invoice.TotalCents = lineItems.TotalCents;
        invoice.UpdatedAt = DateTime.UtcNow;
        // OgmReference, SentAt, DueDate deliberately untouched (FR-011).
        await db.SaveChangesAsync(cancellationToken);

        if (invoice.Status == InvoiceStatus.Sent)
            await notifications.NotifyAsync(invoice, cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);
        return RegenerateInvoiceResult.Success(InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name));
    }
}
