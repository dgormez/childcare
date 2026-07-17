using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// child_milestone_observations (data-model.md, feature 016), tenant schema — append-only. No
// UpdatedAt, no soft-delete column, and deliberately no update/delete MediatR command or
// endpoint exists anywhere for this entity (research.md R3, FR-003): immutability here is
// structural, not policy-enforced. A regression (e.g. achieved -> not_yet) is always a new row.
public class ChildMilestoneObservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    // No DB FK: DevelopmentalMilestone lives in the public schema, which PostgreSQL cannot
    // FK across (research.md R1, same precedent as VaccineRecord.VaccineTypeId).
    public Guid MilestoneId { get; set; }

    public MilestoneObservationStatus Status { get; set; }
    public DateOnly ObservedAt { get; set; }

    // Every StaffProfileId checked in for the location/group at ObservedAt — resolved via
    // IShiftAttributionService, identical shape/column type to ChildEvent.RecordedBy (0, 1, or
    // 2+ entries).
    public List<Guid> ObservedBy { get; set; } = [];

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
