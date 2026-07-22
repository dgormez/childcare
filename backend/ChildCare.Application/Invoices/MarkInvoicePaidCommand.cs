using ChildCare.Application.Common;
using ChildCare.Application.Payments;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-009/FR-013. Only valid on Sent (including overdue) or, since feature
// 026, PendingDebit (an invoice collected via a generated SEPA batch — 026 spec.md FR-009) — a
// one-way transition, no "unmark paid" action in this phase.
public record MarkInvoicePaidCommand(Guid InvoiceId, DateOnly PaidAt) : IRequest<MarkInvoicePaidResult>;

public enum MarkInvoicePaidFailure { NotFound, NotSent }

public class MarkInvoicePaidResult
{
    public InvoiceResponse? Response { get; private init; }
    public MarkInvoicePaidFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static MarkInvoicePaidResult Success(InvoiceResponse response) => new() { Response = response };
    public static MarkInvoicePaidResult Fail(MarkInvoicePaidFailure failure) => new() { Failure = failure };
}

public class MarkInvoicePaidCommandValidator : AbstractValidator<MarkInvoicePaidCommand> { }

// Feature 014a — also triggers PaymentReceiptNotificationService (spec.md FR-015): a receipt
// is generated identically whether an invoice reaches Paid via this manual path or the
// online-payment webhook.
public class MarkInvoicePaidCommandHandler(ITenantDbContext db, PaymentReceiptNotificationService receiptNotificationService)
    : IRequestHandler<MarkInvoicePaidCommand, MarkInvoicePaidResult>
{
    public async Task<MarkInvoicePaidResult> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return MarkInvoicePaidResult.Fail(MarkInvoicePaidFailure.NotFound);
        if (invoice.Status is not (InvoiceStatus.Sent or InvoiceStatus.PendingDebit))
            return MarkInvoicePaidResult.Fail(MarkInvoicePaidFailure.NotSent);

        var paidAt = request.PaidAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = paidAt;
        invoice.UpdatedAt = DateTime.UtcNow;

        // Feature 030 (US3, spec.md FR-009a/research.md R5) — "one payment action covers the
        // whole bundle": every sibling invoice sharing this FamilyGroupId transitions Sent→Paid
        // together in the same transaction. Only Sent siblings are touched — one already Paid
        // (shouldn't happen by construction, since they'd have transitioned together) is left
        // alone rather than erroring the whole request.
        var siblingInvoices = invoice.FamilyGroupId is null
            ? []
            : await db.Invoices
                .Where(i => i.FamilyGroupId == invoice.FamilyGroupId && i.Id != invoice.Id && i.Status == InvoiceStatus.Sent)
                .ToListAsync(cancellationToken);

        foreach (var sibling in siblingInvoices)
        {
            sibling.Status = InvoiceStatus.Paid;
            sibling.PaidAt = paidAt;
            sibling.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        await receiptNotificationService.NotifyAsync(db, invoice, cancellationToken);
        foreach (var sibling in siblingInvoices)
            await receiptNotificationService.NotifyAsync(db, sibling, cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);
        return MarkInvoicePaidResult.Success(InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name));
    }
}
