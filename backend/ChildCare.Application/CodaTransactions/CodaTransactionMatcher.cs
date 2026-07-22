using ChildCare.Application.Common;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.CodaTransactions;

/// <summary>
/// A candidate invoice the caller has already resolved from the database — CodaTransactionMatcher
/// itself never queries anything (tasks.md T009), so every candidate it's given is pre-filtered
/// and pre-decrypted by the caller (ImportCodaFileCommand, T018).
/// </summary>
public record CodaInvoiceCandidate(Guid InvoiceId, int TotalCents, InvoiceStatus Status);

public record CodaMatchResult(CodaMatchType MatchType, Guid? MatchedInvoiceId, bool Applied);

/// <summary>
/// Pure matching logic (spec.md FR-004/005/005a/007/008/009/010/016) — unit-testable independent
/// of MediatR/EF Core. The caller is responsible for resolving every candidate below from the
/// database before calling Match: an exact OGM lookup across invoices of any status (so a
/// reference that names an already-Paid invoice can be told apart from one that simply doesn't
/// match anything, per FR-008), the set of open-invoice amount+IBAN candidates (FR-005), an
/// optional already-Paid amount+IBAN candidate from an earlier period (FR-009), and — only when
/// exactOgmMatch is Sent — that invoice's already-received total from prior CodaTransaction rows
/// (FR-010's cumulative-completion math, research.md R5).
///
/// A malformed structured reference (spec.md Edge Cases: right shape, bad checksum) needs no
/// separate handling here — every real Invoice.OgmReference is checksum-valid by construction
/// (OgmReferenceGenerator), so a checksum-failing reference can never equal one by coincidence;
/// the caller's exact-match lookup simply returns null for it, and it falls through to
/// amount+IBAN matching like any other non-matching reference.
/// </summary>
public static class CodaTransactionMatcher
{
    public static CodaMatchResult Match(
        CodaParsedTransaction transaction,
        CodaInvoiceCandidate? exactOgmMatch,
        IReadOnlyList<CodaInvoiceCandidate> openAmountIbanCandidates,
        CodaInvoiceCandidate? closedInvoiceCandidate,
        int alreadyReceivedCentsForExactMatch = 0)
    {
        if (transaction.AmountCents < 0)
            return new CodaMatchResult(CodaMatchType.Reversal, null, Applied: false);

        if (transaction.IsStructuredCommunication && exactOgmMatch is not null)
        {
            if (exactOgmMatch.Status == InvoiceStatus.Paid)
                return new CodaMatchResult(CodaMatchType.Duplicate, exactOgmMatch.InvoiceId, Applied: false);

            var outstandingCents = exactOgmMatch.TotalCents - alreadyReceivedCentsForExactMatch;
            var completesInvoice = transaction.AmountCents >= outstandingCents;
            return new CodaMatchResult(CodaMatchType.Ogm, exactOgmMatch.InvoiceId, Applied: completesInvoice);
        }

        if (openAmountIbanCandidates.Count == 1)
            return new CodaMatchResult(CodaMatchType.IbanAmount, openAmountIbanCandidates[0].InvoiceId, Applied: false);

        // FR-005a — more than one open candidate: never guess, leave unmatched.
        if (openAmountIbanCandidates.Count > 1)
            return new CodaMatchResult(CodaMatchType.Unmatched, null, Applied: false);

        if (closedInvoiceCandidate is not null)
            return new CodaMatchResult(CodaMatchType.ClosedInvoice, closedInvoiceCandidate.InvoiceId, Applied: false);

        return new CodaMatchResult(CodaMatchType.Unmatched, null, Applied: false);
    }
}
