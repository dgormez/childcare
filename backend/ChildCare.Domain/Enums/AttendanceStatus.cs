namespace ChildCare.Domain.Enums;

public enum AttendanceStatus
{
    Present,
    Absent,

    // Set only by a future feature 011 mechanism (closure-calendar bulk job) — never created or
    // transitioned into directly by this feature's own commands (spec.md FR-015).
    Closure,
}
