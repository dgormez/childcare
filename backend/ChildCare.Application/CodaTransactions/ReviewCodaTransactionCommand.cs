using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.CodaTransactions;

// Feature 025 — contracts/coda-payment-matching-api.md, spec.md FR-012. Never touches
// MatchedInvoiceId or any invoice — purely dismisses a row from the "needs review" queue.
public record ReviewCodaTransactionCommand(Guid TransactionId, Guid ReviewedByUserId) : IRequest<ReviewCodaTransactionResult>;

public enum ReviewCodaTransactionFailure { NotFound, NotReviewable }

public class ReviewCodaTransactionResult
{
    public CodaTransactionResponse? Response { get; private init; }
    public ReviewCodaTransactionFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static ReviewCodaTransactionResult Success(CodaTransactionResponse response) => new() { Response = response };
    public static ReviewCodaTransactionResult Fail(ReviewCodaTransactionFailure failure) => new() { Failure = failure };
}

public class ReviewCodaTransactionCommandValidator : AbstractValidator<ReviewCodaTransactionCommand> { }

public class ReviewCodaTransactionCommandHandler(ITenantDbContext db) : IRequestHandler<ReviewCodaTransactionCommand, ReviewCodaTransactionResult>
{
    private static readonly CodaMatchType[] ReviewableTypes = [CodaMatchType.Unmatched, CodaMatchType.Duplicate, CodaMatchType.ClosedInvoice];

    public async Task<ReviewCodaTransactionResult> Handle(ReviewCodaTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await db.CodaTransactions.FirstOrDefaultAsync(t => t.Id == request.TransactionId, cancellationToken);
        if (transaction is null)
            return ReviewCodaTransactionResult.Fail(ReviewCodaTransactionFailure.NotFound);

        if (!ReviewableTypes.Contains(transaction.MatchType) || transaction.ReviewedAt is not null)
            return ReviewCodaTransactionResult.Fail(ReviewCodaTransactionFailure.NotReviewable);

        transaction.ReviewedAt = DateTime.UtcNow;
        transaction.ReviewedByUserId = request.ReviewedByUserId;
        await db.SaveChangesAsync(cancellationToken);

        return ReviewCodaTransactionResult.Success(await CodaTransactionMapper.ToResponseAsync(db, transaction, cancellationToken));
    }
}
