using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

/// <summary>
/// FR-004: live BKR ratio per group, right now — extends
/// <see cref="ChildCare.Application.Attendance.GetBkrRatioQuery"/>'s location-scoped computation
/// to group scope (research.md R2), reusing its exact threshold/status rules via
/// <see cref="ReportingMapper"/>.
/// </summary>
public record GetGroupBkrRatioQuery(Guid? LocationId) : IRequest<BkrRatioOverviewResponse>;

public class GetGroupBkrRatioQueryHandler(ITenantDbContext db, CloseStaleShiftsHelper closeStaleShifts)
    : IRequestHandler<GetGroupBkrRatioQuery, BkrRatioOverviewResponse>
{
    public async Task<BkrRatioOverviewResponse> Handle(GetGroupBkrRatioQuery request, CancellationToken cancellationToken)
    {
        var today = BelgianCalendarDay.Today();
        var asOf = DateTime.UtcNow;

        var locationsQuery = db.Locations.Where(l => l.DeactivatedAt == null);
        if (request.LocationId is not null)
            locationsQuery = locationsQuery.Where(l => l.Id == request.LocationId);
        var locationIds = await locationsQuery.Select(l => l.Id).ToListAsync(cancellationToken);

        foreach (var locationId in locationIds)
            await closeStaleShifts.CloseStaleShiftsAsync(locationId, asOf, cancellationToken);

        var groups = await db.Groups.Where(g => locationIds.Contains(g.LocationId)).ToListAsync(cancellationToken);

        var responses = new List<BkrGroupRatioResponse>();
        foreach (var group in groups)
        {
            var presentChildIds = await db.AttendanceRecords
                .Where(r => r.LocationId == group.LocationId && r.Date == today
                            && r.Status == AttendanceStatus.Present && r.CheckOutAt == null)
                .Select(r => r.ChildId)
                .ToListAsync(cancellationToken);

            var groupPresentChildIds = presentChildIds.Count == 0
                ? []
                : await db.ChildGroupAssignments
                    .Where(a => a.GroupId == group.Id && presentChildIds.Contains(a.ChildId)
                                && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
                    .Select(a => a.ChildId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            var presentCount = groupPresentChildIds.Count;

            var qualifiedStaffCount = await db.RoomShifts
                .Where(s => s.GroupId == group.Id && s.CheckedOutAt == null)
                .Join(db.StaffProfiles, s => s.StaffProfileId, p => p.Id, (s, p) => p)
                .Where(p => p.QualificationLevel != QualificationLevel.StudentVolunteer)
                .Select(p => p.Id)
                .Distinct()
                .CountAsync(cancellationToken);

            var nappingCount = presentCount == 0
                ? 0
                : await db.ChildEvents.CountAsync(
                    e => groupPresentChildIds.Contains(e.ChildId) && e.EventType == ChildEventType.Sleep
                         && e.EndedAt == null && e.DeletedAt == null,
                    cancellationToken);
            var isNapTime = presentCount > 0 && nappingCount * 2 >= presentCount;

            var threshold = ReportingMapper.ComputeBkrThreshold(qualifiedStaffCount, isNapTime);
            var status = ReportingMapper.ComputeBkrStatus(presentCount, qualifiedStaffCount, threshold);

            responses.Add(new BkrGroupRatioResponse(
                group.Id, group.LocationId, presentCount, qualifiedStaffCount, isNapTime, threshold, status));
        }

        return new BkrRatioOverviewResponse(asOf, responses);
    }
}
