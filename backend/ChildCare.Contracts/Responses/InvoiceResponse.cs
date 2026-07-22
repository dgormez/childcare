namespace ChildCare.Contracts.Responses;

// Feature 014 — contracts/014-invoicing/invoicing-api.md.
public record InvoiceResponse(
    Guid Id,
    Guid ChildId,
    string ChildName,
    Guid ContractId,
    Guid LocationId,
    string LocationName,
    DateOnly PeriodMonth,
    string Status,
    bool IsOverdue,
    int SubtotalCents,
    int TotalCents,
    InvoiceLineItemsResponse LineItems,
    string OgmReference,
    DateOnly? DueDate,
    DateTime? SentAt,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? FamilyGroupId,
    // Feature 026 — set while Status is "pendingdebit" (from a generated SEPA batch); reason is
    // populated once the debit is marked returned (FR-010).
    Guid? SepaBatchId,
    string? SepaReturnReason);

public record InvoiceLineItemsResponse(
    int PresentDays,
    int UnjustifiedAbsentDays,
    int DailyRateCents,
    int ClosureDaysExcluded,
    int DaysMin5u,
    int DaysMin11u,
    IReadOnlyList<InvoiceExtraChargeResponse> ExtraCharges);

public record InvoiceExtraChargeResponse(string Label, int AmountCents);

// Feature 030 — contracts/family-siblings-api.md. Presentation shape for a group of invoices
// sharing a FamilyGroupId; the underlying per-child Invoice rows are unchanged (spec.md
// Clarifications).
// InvoiceId lets a client target one of the group's underlying per-child invoices (e.g. to pay
// the whole bundle online — CreatePaymentLinkCommand resolves the full FamilyGroupId total from
// any one of them) without a separate "family invoice" identifier existing anywhere.
public record FamilyInvoiceChildLineResponse(Guid InvoiceId, Guid ChildId, string ChildName, int SubtotalCents);

public record FamilyInvoiceResponse(
    Guid FamilyGroupId,
    IReadOnlyList<FamilyInvoiceChildLineResponse> Children,
    int TotalCents,
    string Status,
    bool IsOverdue,
    DateOnly? DueDate,
    DateTime CreatedAt);
