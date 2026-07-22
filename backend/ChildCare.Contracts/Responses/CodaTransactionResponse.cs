namespace ChildCare.Contracts.Responses;

// Feature 025 — contracts/coda-payment-matching-api.md.
public record CodaImportSummaryResponse(
    Guid ImportId,
    int TransactionCount,
    int SkippedDuplicateCount,
    CodaImportSummaryCountsResponse Summary);

public record CodaImportSummaryCountsResponse(
    int Ogm,
    int IbanAmountSuggested,
    int Unmatched,
    int Duplicate,
    int ClosedInvoice,
    int Reversal);

// SenderIbanMasked is derived from SenderIbanLast4 only — the full IBAN never serializes to the
// client (spec.md FR-014).
public record CodaTransactionResponse(
    Guid Id,
    Guid ImportId,
    DateOnly ValueDate,
    int AmountCents,
    string SenderIbanMasked,
    string SenderName,
    string Communication,
    string MatchType,
    bool Applied,
    CodaMatchedInvoiceResponse? MatchedInvoice,
    DateTime? ReviewedAt);

public record CodaMatchedInvoiceResponse(Guid Id, string ChildName, int TotalCents, int ReceivedCents);
