using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.CodaTransactions;

internal static class CodaTransactionMapper
{
    public static async Task<CodaTransactionResponse> ToResponseAsync(ITenantDbContext db, CodaTransaction t, CancellationToken cancellationToken)
    {
        CodaMatchedInvoiceResponse? matchedInvoice = null;
        if (t.MatchedInvoiceId is { } invoiceId)
        {
            var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
            if (invoice is not null)
            {
                var child = await db.Children.FirstOrDefaultAsync(c => c.Id == invoice.ChildId, cancellationToken);
                var receivedCents = await db.CodaTransactions
                    .Where(x => x.MatchedInvoiceId == invoiceId && x.MatchType == CodaMatchType.Ogm)
                    .SumAsync(x => (int?)x.AmountCents, cancellationToken) ?? 0;
                matchedInvoice = new CodaMatchedInvoiceResponse(
                    invoice.Id, child is null ? string.Empty : $"{child.FirstName} {child.LastName}", invoice.TotalCents, receivedCents);
            }
        }

        return new CodaTransactionResponse(
            t.Id, t.ImportId, t.ValueDate, t.AmountCents, $"•••• {t.SenderIbanLast4}", t.SenderName, t.Communication,
            t.MatchType.ToString().ToLowerInvariant(), t.Applied, matchedInvoice, t.ReviewedAt);
    }
}
