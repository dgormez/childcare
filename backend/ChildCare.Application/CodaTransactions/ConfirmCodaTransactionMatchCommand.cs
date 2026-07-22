using ChildCare.Application.Common;
using ChildCare.Application.Invoices;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.CodaTransactions;

// Feature 025 — contracts/coda-payment-matching-api.md, spec.md FR-006.
public record ConfirmCodaTransactionMatchCommand(Guid TransactionId) : IRequest<ConfirmCodaTransactionMatchResult>;

public enum ConfirmCodaTransactionMatchFailure { NotFound, NotConfirmable }

public class ConfirmCodaTransactionMatchResult
{
    public CodaTransactionResponse? Response { get; private init; }
    public ConfirmCodaTransactionMatchFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static ConfirmCodaTransactionMatchResult Success(CodaTransactionResponse response) => new() { Response = response };
    public static ConfirmCodaTransactionMatchResult Fail(ConfirmCodaTransactionMatchFailure failure) => new() { Failure = failure };
}

public class ConfirmCodaTransactionMatchCommandValidator : AbstractValidator<ConfirmCodaTransactionMatchCommand> { }

public class ConfirmCodaTransactionMatchCommandHandler(ITenantDbContext db, IMediator mediator)
    : IRequestHandler<ConfirmCodaTransactionMatchCommand, ConfirmCodaTransactionMatchResult>
{
    public async Task<ConfirmCodaTransactionMatchResult> Handle(ConfirmCodaTransactionMatchCommand request, CancellationToken cancellationToken)
    {
        var transaction = await db.CodaTransactions.FirstOrDefaultAsync(t => t.Id == request.TransactionId, cancellationToken);
        if (transaction is null)
            return ConfirmCodaTransactionMatchResult.Fail(ConfirmCodaTransactionMatchFailure.NotFound);

        if (transaction.MatchType != CodaMatchType.IbanAmount || transaction.Applied || transaction.MatchedInvoiceId is null)
            return ConfirmCodaTransactionMatchResult.Fail(ConfirmCodaTransactionMatchFailure.NotConfirmable);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == transaction.MatchedInvoiceId.Value, cancellationToken);

        // Stale suggestion — the invoice was independently paid (or otherwise moved) since this
        // suggestion was computed (spec.md Edge Cases). Never applied; re-surfaced instead of
        // silently failing, subject to the same already-paid guard as FR-008.
        if (invoice is null || invoice.Status != InvoiceStatus.Sent)
        {
            if (invoice is { Status: InvoiceStatus.Paid })
            {
                transaction.MatchType = CodaMatchType.Duplicate;
            }
            else
            {
                transaction.MatchType = CodaMatchType.Unmatched;
                transaction.MatchedInvoiceId = null;
            }

            await db.SaveChangesAsync(cancellationToken);
            return ConfirmCodaTransactionMatchResult.Fail(ConfirmCodaTransactionMatchFailure.NotConfirmable);
        }

        await mediator.Send(new MarkInvoicePaidCommand(invoice.Id, transaction.ValueDate), cancellationToken);
        transaction.Applied = true;
        await db.SaveChangesAsync(cancellationToken);

        return ConfirmCodaTransactionMatchResult.Success(await CodaTransactionMapper.ToResponseAsync(db, transaction, cancellationToken));
    }
}
