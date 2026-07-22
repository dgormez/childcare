using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.CodaTransactions;

// Feature 025 — contracts/coda-payment-matching-api.md, spec.md FR-006.
public record RejectCodaTransactionMatchCommand(Guid TransactionId) : IRequest<RejectCodaTransactionMatchResult>;

public enum RejectCodaTransactionMatchFailure { NotFound, NotConfirmable }

public class RejectCodaTransactionMatchResult
{
    public CodaTransactionResponse? Response { get; private init; }
    public RejectCodaTransactionMatchFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static RejectCodaTransactionMatchResult Success(CodaTransactionResponse response) => new() { Response = response };
    public static RejectCodaTransactionMatchResult Fail(RejectCodaTransactionMatchFailure failure) => new() { Failure = failure };
}

public class RejectCodaTransactionMatchCommandValidator : AbstractValidator<RejectCodaTransactionMatchCommand> { }

public class RejectCodaTransactionMatchCommandHandler(ITenantDbContext db)
    : IRequestHandler<RejectCodaTransactionMatchCommand, RejectCodaTransactionMatchResult>
{
    public async Task<RejectCodaTransactionMatchResult> Handle(RejectCodaTransactionMatchCommand request, CancellationToken cancellationToken)
    {
        var transaction = await db.CodaTransactions.FirstOrDefaultAsync(t => t.Id == request.TransactionId, cancellationToken);
        if (transaction is null)
            return RejectCodaTransactionMatchResult.Fail(RejectCodaTransactionMatchFailure.NotFound);

        if (transaction.MatchType != CodaMatchType.IbanAmount || transaction.Applied)
            return RejectCodaTransactionMatchResult.Fail(RejectCodaTransactionMatchFailure.NotConfirmable);

        transaction.MatchType = CodaMatchType.Unmatched;
        transaction.MatchedInvoiceId = null;
        await db.SaveChangesAsync(cancellationToken);

        return RejectCodaTransactionMatchResult.Success(await CodaTransactionMapper.ToResponseAsync(db, transaction, cancellationToken));
    }
}
