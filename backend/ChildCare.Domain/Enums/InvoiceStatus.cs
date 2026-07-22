namespace ChildCare.Domain.Enums;

// Status only ever moves forward: Draft -> Sent -> Paid (spec.md FR-013). "Overdue" is not a
// stored value — it's computed as Status == Sent && DueDate < today (research.md R4).
// Feature 026 inserts PendingDebit between Sent and Paid, with one explicit backward exception:
// PendingDebit -> Sent on a returned SEPA debit (026 spec.md FR-010).
public enum InvoiceStatus
{
    Draft,
    Sent,
    PendingDebit,
    Paid,
}
