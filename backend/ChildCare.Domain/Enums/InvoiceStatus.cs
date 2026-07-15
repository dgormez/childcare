namespace ChildCare.Domain.Enums;

// Status only ever moves forward: Draft -> Sent -> Paid (spec.md FR-013). "Overdue" is not a
// stored value — it's computed as Status == Sent && DueDate < today (research.md R4).
public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
}
