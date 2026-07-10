using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// staff_schedules (data-model.md) — a planned shift for one staff member. Distinct from
// RoomShift (feature 008a): this is the *planned* rota, RoomShift is the *actual*
// check-in/out presence log. Feature 010's live BKR ratio reads RoomShift only, never this
// table (research.md R1) — this entity feeds a separate, planning-only projected-on-duty
// count (GetProjectedOnDutyQuery).
public class StaffSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? GroupId { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public bool IsAbsent { get; set; }
    public AbsenceReason? AbsenceReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
