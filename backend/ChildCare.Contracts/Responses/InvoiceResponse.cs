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
    DateTime UpdatedAt);

public record InvoiceLineItemsResponse(
    int PresentDays,
    int UnjustifiedAbsentDays,
    int DailyRateCents,
    int ClosureDaysExcluded,
    int DaysMin5u,
    int DaysMin11u,
    IReadOnlyList<InvoiceExtraChargeResponse> ExtraCharges);

public record InvoiceExtraChargeResponse(string Label, int AmountCents);
