using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

/// <summary>
/// FR-007/007a-e: live BKR (begeleider-kind-ratio) computation for a location, right now.
/// Present count and qualified-on-duty-staff are both derived from data this feature (and
/// features 008a/009) already write — no new state is stored (data-model.md's BKR Ratio,
/// research.md R2).
/// </summary>
public record GetBkrRatioQuery(Guid LocationId) : IRequest<BkrRatioResponse>;

public class GetBkrRatioQueryHandler(ITenantDbContext db, CloseStaleShiftsHelper closeStaleShifts)
    : IRequestHandler<GetBkrRatioQuery, BkrRatioResponse>
{
    public async Task<BkrRatioResponse> Handle(GetBkrRatioQuery request, CancellationToken cancellationToken)
    {
        await closeStaleShifts.CloseStaleShiftsAsync(request.LocationId, DateTime.UtcNow, cancellationToken);

        var today = BelgianCalendarDay.Today();

        // FR-007d: only status=present with no check-out counts as currently present.
        var presentChildIds = await db.AttendanceRecords
            .Where(r => r.LocationId == request.LocationId && r.Date == today
                        && r.Status == AttendanceStatus.Present && r.CheckOutAt == null)
            .Select(r => r.ChildId)
            .ToListAsync(cancellationToken);
        var presentCount = presentChildIds.Count;

        // FR-007a: StudentVolunteer never counts, even if checked in. A director covering a
        // shift with no QualificationLevel set is not excluded — only the explicit
        // StudentVolunteer case is (spec.md FR-007a's literal exclusion criterion).
        var qualifiedStaffCount = await db.RoomShifts
            .Where(s => s.LocationId == request.LocationId && s.CheckedOutAt == null)
            .Join(db.StaffProfiles, s => s.StaffProfileId, p => p.Id, (s, p) => p)
            .Where(p => p.QualificationLevel != QualificationLevel.StudentVolunteer)
            .Select(p => p.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        // FR-007c: nap time is inferred when at least half (rounding up) of present children
        // have an open sleep event right now — nappingCount * 2 >= presentCount.
        var nappingCount = presentCount == 0
            ? 0
            : await db.ChildEvents.CountAsync(
                e => presentChildIds.Contains(e.ChildId) && e.EventType == ChildEventType.Sleep
                     && e.EndedAt == null && e.DeletedAt == null,
                cancellationToken);
        var isNapTime = presentCount > 0 && nappingCount * 2 >= presentCount;

        var perCaregiverCap = isNapTime ? 14 : (qualifiedStaffCount <= 1 ? 8 : 9);
        var threshold = perCaregiverCap * Math.Max(qualifiedStaffCount, 1);

        string status;
        if (qualifiedStaffCount == 0 && presentCount > 0)
            status = "red";
        else if (presentCount < threshold)
            status = "green";
        else if (presentCount == threshold)
            status = "amber";
        else
            status = "red";

        return new BkrRatioResponse(presentCount, qualifiedStaffCount, isNapTime, threshold, status);
    }
}
