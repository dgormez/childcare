namespace ChildCare.Contracts.Requests;

// Feature 014 — contracts/014-invoicing/invoicing-api.md.
public record GenerateInvoicesRequest(int Year, int Month);

public record UpdateInvoiceExtraChargesRequest(IReadOnlyList<InvoiceExtraChargeRequest> ExtraCharges);

public record InvoiceExtraChargeRequest(string Label, int AmountCents);

public record SendInvoicesRequest(IReadOnlyList<Guid> InvoiceIds);

public record MarkInvoicePaidRequest(DateOnly PaidAt);

// Feature 026 — contracts/sepa-direct-debit-api.md, spec.md FR-010.
public record MarkInvoiceSepaReturnedRequest(string Reason);
