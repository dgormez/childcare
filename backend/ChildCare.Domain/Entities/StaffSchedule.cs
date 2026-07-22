using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// staff_schedules (data-model.md) — a planned shift for one staff member. Distinct from
// RoomShift (feature 008a): this is the *planned* rota, RoomShift is the *actual*
// check-in/out presence log. Feature 010's live BKR ratio reads RoomShift only, never this
// table (research.md R1) — this entity feeds a separate, planning-only projected-on-duty
// count (GetProjectedOnDutyQuery).
//
// Feature 027 extends this in place (research.md R1/R3/R4) with Status/CoverStaffId/Notes/
// CreatedBy/IsPublished/PublishedAt rather than a parallel staff_assignments table. IsAbsent
// is no longer a persisted column — see the computed property below.
public class StaffSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? GroupId { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public StaffScheduleStatus Status { get; set; } = StaffScheduleStatus.Scheduled;
    public AbsenceReason? AbsenceReason { get; set; }

    // Not mapped (TenantDbContext.cs) — a computed convenience over Status, preserving every
    // existing IsAbsent call site (GetProjectedOnDutyQuery etc.) without a rename ripple
    // (research.md R3, data-model.md).
    public bool IsAbsent => Status == StaffScheduleStatus.Absent;

    // Who covered this ABSENT row (FR-007) — set on the original row, pointing at the
    // replacement's StaffProfileId. Corrected placement from spec.md's Key Entities wording
    // (data-model.md) — the replacement's own new row does not self-reference.
    public Guid? CoverStaffId { get; set; }

    public string? Notes { get; set; }

    // Which director created/last changed this row (data-model.md).
    public Guid? CreatedBy { get; set; }

    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
