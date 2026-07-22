using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// staff_leave_requests (data-model.md, new) — a staff-initiated request for planned time off
// (or, via ReportSickCommand's side effect, an auto-approved same-day sick record for the
// "Verlofaanvragen" history/audit trail). Tenant-schema, same structural pattern as
// StaffSchedule — no explicit tenant FK column (constitution Principle I).
public class StaffLeaveRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }

    public StaffLeaveRequestType Type { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string? Notes { get; set; }

    public StaffLeaveRequestStatus Status { get; set; } = StaffLeaveRequestStatus.Pending;
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
