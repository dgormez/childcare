using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Attendance;

internal static class AttendanceMapper
{
    public static AttendanceRecordResponse ToResponse(Domain.Entities.AttendanceRecord r) => new(
        r.Id,
        r.ChildId,
        r.LocationId,
        r.Date,
        r.Status.ToString().ToLowerInvariant(),
        r.CheckInAt,
        r.CheckOutAt,
        r.PlannedDurationMinutes,
        r.AbsenceJustified,
        r.AbsenceReason,
        r.RecordedBy,
        r.CreatedAt,
        r.UpdatedAt);
}
