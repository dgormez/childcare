using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// attendance_records (data-model.md) — one row per child per location per calendar day
// (unique constraint), the daily presence register feature 010 builds.
public class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public Guid LocationId { get; set; }

    // Europe/Brussels-anchored calendar day (BelgianCalendarDay, feature 009) this record is for.
    public DateOnly Date { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }

    // Derived once at check-in from the child's active Contract at this LocationId for this
    // weekday (research.md R6); null when no matching ContractedDay exists (an "extra day").
    public int? PlannedDurationMinutes { get; set; }

    // Null unless Status = Absent.
    public bool? AbsenceJustified { get; set; }
    public string? AbsenceReason { get; set; }

    // Every StaffProfileId checked in (via IShiftAttributionService) at the moment of the
    // action — mirrors ChildEvent.RecordedBy's precedent (spec.md FR-014). For a director
    // correction, the director's own TenantUserId wrapped in a single-element array.
    public List<Guid> RecordedBy { get; set; } = [];

    public Guid? ClosureDayId { get; set; }
    public string? PriorStateJson { get; set; }
    public Guid? ClosureConfirmedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
