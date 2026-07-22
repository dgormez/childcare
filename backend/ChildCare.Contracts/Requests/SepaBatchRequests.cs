namespace ChildCare.Contracts.Requests;

// Feature 026 — contracts/sepa-direct-debit-api.md.
public record GenerateSepaBatchRequest(IReadOnlyList<Guid> InvoiceIds, DateOnly ExecutionDate);
