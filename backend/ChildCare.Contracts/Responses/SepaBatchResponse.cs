namespace ChildCare.Contracts.Responses;

// Feature 026 — contracts/sepa-direct-debit-api.md.
public record SepaBatchEligibilityResponse(
    bool CreditorConfigured,
    IReadOnlyList<SepaBatchEligibleInvoiceResponse> Eligible,
    IReadOnlyList<SepaBatchExcludedInvoiceResponse> Excluded);

public record SepaBatchEligibleInvoiceResponse(Guid InvoiceId, string ChildName, int TotalCents, string DebtorName);

// Reason is one of "NoMandate", "MandateRevoked", "NonPositiveAmount" (data-model.md's priority
// order).
public record SepaBatchExcludedInvoiceResponse(Guid InvoiceId, string ChildName, int TotalCents, string Reason);

public record SepaBatchResponse(
    Guid Id,
    DateOnly ExecutionDate,
    DateTime GeneratedAt,
    int InvoiceCount,
    int TotalCents);
