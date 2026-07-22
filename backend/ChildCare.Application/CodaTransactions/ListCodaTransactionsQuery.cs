using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.CodaTransactions;

// Feature 025 — contracts/coda-payment-matching-api.md. matchType/needsReview are mutually
// independent filters (both may be supplied); needsReview is FR-012's review-queue shorthand for
// Unmatched/Duplicate/ClosedInvoice rows with ReviewedAt still null.
public record ListCodaTransactionsQuery(CodaMatchType? MatchType, bool? NeedsReview) : IRequest<IReadOnlyList<CodaTransactionResponse>>;

public class ListCodaTransactionsQueryHandler(ITenantDbContext db) : IRequestHandler<ListCodaTransactionsQuery, IReadOnlyList<CodaTransactionResponse>>
{
    private static readonly CodaMatchType[] ReviewableTypes = [CodaMatchType.Unmatched, CodaMatchType.Duplicate, CodaMatchType.ClosedInvoice];

    public async Task<IReadOnlyList<CodaTransactionResponse>> Handle(ListCodaTransactionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.CodaTransactions.AsQueryable();

        if (request.MatchType is { } matchType)
            query = query.Where(t => t.MatchType == matchType);

        if (request.NeedsReview == true)
            query = query.Where(t => ReviewableTypes.Contains(t.MatchType) && t.ReviewedAt == null);

        var transactions = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

        var invoiceIds = transactions.Where(t => t.MatchedInvoiceId is not null).Select(t => t.MatchedInvoiceId!.Value).Distinct().ToList();
        var invoices = await db.Invoices
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, i.TotalCents, i.ChildId })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var childIds = invoices.Values.Select(i => i.ChildId).Distinct().ToList();
        var childNames = await db.Children
            .Where(c => childIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}", cancellationToken);

        var receivedByInvoice = await db.CodaTransactions
            .Where(t => t.MatchedInvoiceId != null && t.MatchType == CodaMatchType.Ogm)
            .GroupBy(t => t.MatchedInvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, ReceivedCents = g.Sum(t => t.AmountCents) })
            .ToDictionaryAsync(g => g.InvoiceId, g => g.ReceivedCents, cancellationToken);

        return transactions.Select(t =>
        {
            CodaMatchedInvoiceResponse? matchedInvoice = null;
            if (t.MatchedInvoiceId is { } invoiceId && invoices.TryGetValue(invoiceId, out var invoice))
            {
                matchedInvoice = new CodaMatchedInvoiceResponse(
                    invoice.Id,
                    childNames.GetValueOrDefault(invoice.ChildId, string.Empty),
                    invoice.TotalCents,
                    receivedByInvoice.GetValueOrDefault(invoiceId));
            }

            return new CodaTransactionResponse(
                t.Id,
                t.ImportId,
                t.ValueDate,
                t.AmountCents,
                $"•••• {t.SenderIbanLast4}",
                t.SenderName,
                t.Communication,
                t.MatchType.ToString().ToLowerInvariant(),
                t.Applied,
                matchedInvoice,
                t.ReviewedAt);
        }).ToList();
    }
}
