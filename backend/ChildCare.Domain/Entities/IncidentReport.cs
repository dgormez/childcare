using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// incident_reports (data-model.md) — a legal, largely-immutable record of a single safety-relevant
// event involving one child (Besluit Kwaliteit Kinderopvang record-keeping requirement, feature
// 013b). Never cascade-deleted or hidden when its child is deactivated (FR-008); never physically
// deleted at all (no Cancelled/Deleted state — legal document retention).
public class IncidentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    // Resolved from the filing device's paired location (feature 008a) — the filter/index
    // dimension for FR-009/FR-017, additive to the original BACKLOG schema (FR-019).
    public Guid LocationId { get; set; }

    // Caregiver-set, can be backdated (FR-003); CreatedAt (not this field) starts the 24h
    // immutability clock (FR-005).
    public DateTime OccurredAt { get; set; }

    public string? LocationDetail { get; set; }

    public string Description { get; set; } = string.Empty;
    public IncidentInjuryType InjuryType { get; set; }

    public string? FirstAidGiven { get; set; }
    public bool DoctorCalled { get; set; }
    public string? DoctorNotes { get; set; }

    public bool ParentNotified { get; set; }
    public DateTime? ParentNotifiedAt { get; set; }
    public ParentNotifiedHow? ParentNotifiedHow { get; set; }

    // 0, 1, or 2+ caregiver ids — resolved server-side via IShiftAttributionService (FR-004),
    // never client-submitted. Not a formal FK list, same pattern as ChildEvent.RecordedBy.
    public List<Guid> ReportedBy { get; set; } = [];

    public string? Witnesses { get; set; }

    // The only field editable after the 24-hour lock (FR-006).
    public string? FollowUp { get; set; }

    // Null = unreviewed. Set on first director detail-view read (FR-010/FR-011); never reset by
    // subsequent edits (spec Clarifications).
    public DateTime? ReviewedAt { get; set; }

    // Immutability clock reference (FR-005) — not OccurredAt.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
