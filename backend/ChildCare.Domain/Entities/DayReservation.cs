using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// day_reservations (data-model.md) — parent-submitted absence/extra/exchange requests with a
// director approval queue (feature 013a). Approving an Absence writes a separate
// AttendanceRecord (feature 010, via MarkAbsentCommand) rather than storing attendance state
// here; Extra/Exchange approvals only transition this row's own Status (research.md R1/R2).
public class DayReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    public DayReservationType Type { get; set; }
    public DateOnly RequestedDate { get; set; }

    // Only set when Type = Exchange — the contracted day being given up.
    public DateOnly? ExchangeForDate { get; set; }

    public string? Reason { get; set; }

    // Only meaningful for Type = Absence, set by the director at approval time (FR-008).
    public bool? AbsenceJustified { get; set; }

    public DayReservationStatus Status { get; set; } = DayReservationStatus.Pending;

    public Guid RequestedBy { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DirectorNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
