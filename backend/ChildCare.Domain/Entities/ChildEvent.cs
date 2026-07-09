using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// child_events (data-model.md) — one row per recorded occurrence across 11 event types, a
// single JSONB-backed table per constitution's Development Workflow section (no per-type table).
public class ChildEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    // Sourced from the recording device's claims at write time (same values
    // IShiftAttributionService needs) — what FR-006's edit-window check authorizes against.
    public Guid LocationId { get; set; }
    public Guid GroupId { get; set; }

    public ChildEventType EventType { get; set; }
    public DateTime OccurredAt { get; set; }

    // Sleep only; null = in progress.
    public DateTime? EndedAt { get; set; }

    // Raw JSON matching EventType's shape (data-model.md Validation Rules table) — validated in
    // the Application layer, not by the database (research.md R1).
    public string Payload { get; set; } = "{}";

    public bool VisibleToParent { get; set; } = true;

    // 0, 1, or 2+ StaffProfileIds — resolved via IShiftAttributionService at write time
    // (research.md R2). Empty if nobody was checked in yet.
    public List<Guid> RecordedBy { get; set; } = [];

    // Medication/temperature only. Set via the reused confirm-administrator flow; null if
    // skipped or recorded offline, director-fillable later (FR-016).
    public Guid? AdministeredBy { get; set; }

    public Guid RecordedByDeviceId { get; set; }

    // Soft-delete: null = active.
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
