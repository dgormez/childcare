namespace ChildCare.Domain.Enums;

// data-model.md — Pending -> Approved/Rejected, terminal (no re-opening a decided request).
public enum StaffLeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
}
