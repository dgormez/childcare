using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// group_activities (data-model.md) — a single shared moment recorded once for an entire group,
// distinct from ChildEvent, which is always scoped to one child. No UpdatedAt/DeletedAt: this
// entity has no edit path (spec.md FR-014) and uses hard delete, not soft delete (spec.md
// Assumptions), so those columns would be permanently unused.
public class GroupActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid LocationId { get; set; }

    public GroupActivityType ActivityType { get; set; } = GroupActivityType.Other;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime OccurredAt { get; set; }

    // 0, 1, or 2+ StaffProfileIds — resolved via IShiftAttributionService at write time
    // (research.md R1), same pattern as ChildEvent.RecordedBy. Empty if nobody was checked in.
    public List<Guid> RecordedBy { get; set; } = [];

    public Guid RecordedByDeviceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
