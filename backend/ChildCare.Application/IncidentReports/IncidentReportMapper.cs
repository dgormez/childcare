using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.IncidentReports;

internal static class IncidentReportMapper
{
    public static IncidentReportResponse ToResponse(IncidentReport r) => new(
        r.Id,
        r.ChildId,
        r.LocationId,
        r.OccurredAt,
        r.LocationDetail,
        r.Description,
        r.InjuryType.ToWireString(),
        r.FirstAidGiven,
        r.DoctorCalled,
        r.DoctorNotes,
        r.ParentNotified,
        r.ParentNotifiedAt,
        r.ParentNotifiedHow?.ToWireString(),
        r.ReportedBy,
        r.Witnesses,
        r.FollowUp,
        r.ReviewedAt,
        r.CreatedAt,
        r.UpdatedAt);
}
