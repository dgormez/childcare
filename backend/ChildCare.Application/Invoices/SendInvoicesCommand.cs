using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-005a/FR-007/FR-013. All-or-nothing: if any requested invoice is not
// currently Draft, the whole request is rejected and nothing changes.
public record SendInvoicesCommand(IReadOnlyList<Guid> InvoiceIds) : IRequest<SendInvoicesResult>;

public enum SendInvoicesFailure { NotFound, NotDraft }

public class SendInvoicesResult
{
    public IReadOnlyList<InvoiceResponse>? Responses { get; private init; }
    public SendInvoicesFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static SendInvoicesResult Success(IReadOnlyList<InvoiceResponse> responses) => new() { Responses = responses };
    public static SendInvoicesResult Fail(SendInvoicesFailure failure) => new() { Failure = failure };
}

public class SendInvoicesCommandValidator : AbstractValidator<SendInvoicesCommand>
{
    public SendInvoicesCommandValidator()
    {
        RuleFor(x => x.InvoiceIds).NotEmpty();
    }
}

public class SendInvoicesCommandHandler(ITenantDbContext db, InvoiceNotificationService notifications)
    : IRequestHandler<SendInvoicesCommand, SendInvoicesResult>
{
    public async Task<SendInvoicesResult> Handle(SendInvoicesCommand request, CancellationToken cancellationToken)
    {
        var invoices = await db.Invoices.Where(i => request.InvoiceIds.Contains(i.Id)).ToListAsync(cancellationToken);
        if (invoices.Count != request.InvoiceIds.Count)
            return SendInvoicesResult.Fail(SendInvoicesFailure.NotFound);
        if (invoices.Any(i => i.Status != InvoiceStatus.Draft))
            return SendInvoicesResult.Fail(SendInvoicesFailure.NotDraft);

        var locationIds = invoices.Select(i => i.LocationId).Distinct().ToList();
        var locations = await db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        foreach (var invoice in invoices)
        {
            var location = locations[invoice.LocationId];
            invoice.Status = InvoiceStatus.Sent;
            invoice.SentAt = now;
            invoice.DueDate = today.AddDays(location.InvoiceDueDays);
            invoice.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var invoice in invoices)
            await notifications.NotifyAsync(invoice, cancellationToken);

        var childIds = invoices.Select(i => i.ChildId).Distinct().ToList();
        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var responses = invoices
            .Select(i => InvoiceMapper.ToResponse(i, $"{children[i.ChildId].FirstName} {children[i.ChildId].LastName}", locations[i.LocationId].Name))
            .ToList();
        return SendInvoicesResult.Success(responses);
    }
}
