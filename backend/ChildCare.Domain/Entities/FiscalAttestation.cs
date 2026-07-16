namespace ChildCare.Domain.Entities;

// Feature 015 — data-model.md. One row per (ChildId, LocationId, TaxYear); regenerating
// (spec.md FR-008/FR-009) overwrites this same row in place rather than creating a new one.
// Unlike Invoice's on-demand PDF rendering (014/014a), the PDF here is persisted to GCS
// (research.md R1) — a filed tax document benefits from a stable snapshot, not a live
// recomputation. No FK to Invoice/Contract: Periods is a computed aggregate snapshot, not a
// live reference (research.md R3).
public class FiscalAttestation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChildId { get; set; }
    public Guid LocationId { get; set; }
    public int TaxYear { get; set; }

    // Raw JSON matching a List<FiscalAttestationPeriod> (data-model.md) — up to 4 entries,
    // mirrors Invoice.LineItems' raw-JSON-text precedent (014).
    public string Periods { get; set; } = "[]";

    public int TotalAmountCents { get; set; }

    // GCS object path only, never a URL — signed download URLs are generated fresh on every
    // read (mirrors Child.ProfilePhotoObjectPath's precedent). Always "fiscal-attestations/{Id}.pdf".
    public string PdfObjectPath { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
