using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 026 — contracts/sepa-direct-debit-api.md, spec.md FR-010. Only valid on PendingDebit —
// reverts to Sent (normal overdue follow-up, eligible for a future batch) with a required reason.
// Note SepaMandateReferenceUsed is deliberately left untouched (data-model.md) — it's a permanent
// audit fact for FR-002a's sequence-type resolution, not a current-state pointer like SepaBatchId.
public record MarkInvoiceSepaReturnedCommand(Guid InvoiceId, string Reason) : IRequest<MarkInvoiceSepaReturnedResult>;

public enum MarkInvoiceSepaReturnedFailure { NotFound, NotPendingDebit, ReasonRequired }

public class MarkInvoiceSepaReturnedResult
{
    public InvoiceResponse? Response { get; private init; }
    public MarkInvoiceSepaReturnedFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static MarkInvoiceSepaReturnedResult Success(InvoiceResponse response) => new() { Response = response };
    public static MarkInvoiceSepaReturnedResult Fail(MarkInvoiceSepaReturnedFailure failure) => new() { Failure = failure };
}

public class MarkInvoiceSepaReturnedCommandValidator : AbstractValidator<MarkInvoiceSepaReturnedCommand> { }

public class MarkInvoiceSepaReturnedCommandHandler(ITenantDbContext db) : IRequestHandler<MarkInvoiceSepaReturnedCommand, MarkInvoiceSepaReturnedResult>
{
    public async Task<MarkInvoiceSepaReturnedResult> Handle(MarkInvoiceSepaReturnedCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return MarkInvoiceSepaReturnedResult.Fail(MarkInvoiceSepaReturnedFailure.NotFound);
        if (invoice.Status != InvoiceStatus.PendingDebit)
            return MarkInvoiceSepaReturnedResult.Fail(MarkInvoiceSepaReturnedFailure.NotPendingDebit);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return MarkInvoiceSepaReturnedResult.Fail(MarkInvoiceSepaReturnedFailure.ReasonRequired);

        invoice.Status = InvoiceStatus.Sent;
        invoice.SepaBatchId = null;
        invoice.SepaReturnReason = request.Reason;
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);
        return MarkInvoiceSepaReturnedResult.Success(InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name));
    }
}
