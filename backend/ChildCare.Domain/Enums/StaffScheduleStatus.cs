namespace ChildCare.Domain.Enums;

// data-model.md — feature 027's single source of truth for a StaffSchedule row's state,
// replacing the old IsAbsent bool + AbsenceReason pair (research.md R3).
public enum StaffScheduleStatus
{
    Scheduled,
    Confirmed,
    Absent,
    Covered,
}
