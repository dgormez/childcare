namespace ChildCare.Domain.Enums;

// data-model.md — a staff-initiated leave request's type. Maps onto the existing AbsenceReason
// enum on approval (research.md R3): Sick -> Sick, Annual -> Leave, Other -> Leave.
public enum StaffLeaveRequestType
{
    Sick,
    Annual,
    Other,
}
