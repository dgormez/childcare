namespace ChildCare.Domain.Entities;

// Feature 026, data-model.md. One row per successfully generated pain.008 batch — audit/history
// record (FR-007/FR-008). Immutable once created; a returned debit (FR-010) changes the
// affected invoice, not this record. Included invoices are found via Invoice.SepaBatchId ==
// Id — no separate join table, mirroring Invoice.FamilyGroupId's (feature 030) one-to-many
// grouping without a dedicated join table.
public class SepaBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LocationId { get; set; }

    // Set by the director (FR-002), validated against FR-005 at generation time.
    public DateOnly ExecutionDate { get; set; }

    public Guid GeneratedByUserId { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Denormalized from the included invoices at generation time (FR-008) — must always equal
    // the XML's own NbOfTxs/CtrlSum control totals (FR-006/FR-008), never a second,
    // independently-drifting figure.
    public int TotalCents { get; set; }
    public int InvoiceCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
