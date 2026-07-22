using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// invoices (data-model.md, feature 014) — one row per (ChildId, ContractId, LocationId,
// PeriodMonth). LineItems is raw JSON text, validated/serialized in the Application layer, not
// by the database — mirrors ChildEvent.Payload's existing precedent rather than introducing a
// new EF JSONB-conversion pattern for a single entity.
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Tenant-schema-scoped identity column — feeds the OGM base number only (research.md R3).
    // Never used as a foreign key or exposed as "the" invoice identifier anywhere else; Id
    // remains the real primary key/identifier for every relation and lookup.
    public long SequenceNumber { get; set; }

    public Guid ChildId { get; set; }
    public Guid ContractId { get; set; }
    public Guid LocationId { get; set; }

    // First-of-month date, e.g. 2027-07-01 for July 2027.
    public DateOnly PeriodMonth { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public int SubtotalCents { get; set; }
    public int TotalCents { get; set; }

    // Raw JSON matching InvoiceLineItems' shape (data-model.md) — present/unjustified-absent
    // day counts, daily rate, closure-day count, duration-categorized counts, extra charges.
    public string LineItems { get; set; } = "{}";

    // Assigned exactly once at first creation; never changes afterward (spec.md FR-004).
    public string OgmReference { get; set; } = string.Empty;

    // Null until sent — set once, at send time, as that day's date plus the location's
    // InvoiceDueDays (spec.md FR-005a); never recomputed on regenerate (FR-011).
    public DateOnly? DueDate { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? PaidAt { get; set; }

    // Feature 014a — capped automatic payment-reminder tracking (spec.md FR-013). Frozen once
    // Paid; unaffected by regenerate (spec.md Edge Cases, mirrors DueDate's own regenerate
    // invariant).
    public int ReminderCount { get; set; }
    public DateTime? LastReminderSentAt { get; set; }

    // Feature 030 — null unless generated while the location's FamilyInvoiceBundlingEnabled was
    // true and the child had 2+ siblings sharing the same primary contact (research.md R4). Wraps
    // existing per-child rows for combined PDF/parent-app/payment purposes only — never changes
    // ChildId/ContractId/reporting semantics (spec.md Clarifications).
    public Guid? FamilyGroupId { get; set; }

    // Feature 026 — set when this invoice is included in a generated SEPA batch (026 spec.md
    // FR-007); cleared when it returns to Sent via a returned-debit action (FR-010). Null for
    // every invoice not currently PendingDebit.
    public Guid? SepaBatchId { get; set; }

    // Feature 026 — an immutable snapshot of Contract.SepaMandateReference taken at the moment
    // this invoice was generated into a batch. Unlike SepaBatchId, this is NEVER cleared by a
    // returned debit (026 spec.md FR-010) or anything else — it's a permanent audit fact ("this
    // invoice was once debited under mandate reference X"), not a current-state pointer. Exists
    // solely so SepaSequenceTypeResolver can correctly determine FRST vs RCUR across both a
    // return and a revoke-and-resign (026 research.md R3) — a query against the live, clearable
    // SepaBatchId alone cannot do this (026 data-model.md).
    public string? SepaMandateReferenceUsed { get; set; }

    // Feature 026 — set by the returned-debit action (FR-010) alongside the PendingDebit -> Sent
    // transition.
    public string? SepaReturnReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
