using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-009/FR-013. Only valid on Sent (including overdue) — a one-way
// transition, no "unmark paid" action in this phase.
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

public class MarkInvoicePaidCommandHandler(ITenantDbContext db) : IRequestHandler<MarkInvoicePaidCommand, MarkInvoicePaidResult>
{
    public async Task<MarkInvoicePaidResult> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return MarkInvoicePaidResult.Fail(MarkInvoicePaidFailure.NotFound);
        if (invoice.Status != InvoiceStatus.Sent)
            return MarkInvoicePaidResult.Fail(MarkInvoicePaidFailure.NotSent);

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = request.PaidAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);
        return MarkInvoicePaidResult.Success(InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name));
    }
}
