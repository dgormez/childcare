using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// staff_time_entries (data-model.md, feature 028) — one clock-in/clock-out record per staff
// member, per shift segment. IsOpen/IsLocked are computed, not persisted (data-model.md).
public class StaffTimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? GroupId { get; set; }

    public DateTime ClockedInAt { get; set; }
    public DateTime? ClockedOutAt { get; set; }

    public StaffTimeEntryFunction Function { get; set; }

    public string? Notes { get; set; }

    // Non-null = an active director unlock override (research.md R4) — bypasses the computed
    // 7-day lock (FR-006) until an explicit re-lock clears both fields. Doubles as the FR-007a
    // audit trail (who unlocked it, when).
    public DateTime? UnlockedAt { get; set; }
    public Guid? UnlockedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsOpen => ClockedOutAt is null;

    public bool IsLocked(DateTime utcNow) =>
        UnlockedAt is null && utcNow - ClockedInAt > TimeSpan.FromDays(7);
}
