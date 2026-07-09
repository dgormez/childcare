using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

/// <summary>
/// Device-readable "what's today's attendance state at my own location" lookup — the caregiver
/// tablet's group view needs this to render each child's current present/absent/unset state
/// (and to survive an app restart or another tablet's action), but every other attendance read
/// (ListAttendanceQuery) is DirectorOnly. Discovered as a real gap during implementation, not
/// originally scoped in spec.md — added the same way features 007a/009 added small, additive,
/// read-only endpoints once their UI surfaced a gap prior features had no reason to close.
/// </summary>
public record GetTodayAttendanceQuery(Guid LocationId) : IRequest<IReadOnlyList<AttendanceRecordResponse>>;

public class GetTodayAttendanceQueryHandler(ITenantDbContext db) : IRequestHandler<GetTodayAttendanceQuery, IReadOnlyList<AttendanceRecordResponse>>
{
    public async Task<IReadOnlyList<AttendanceRecordResponse>> Handle(GetTodayAttendanceQuery request, CancellationToken cancellationToken)
    {
        var today = BelgianCalendarDay.Today();
        var records = await db.AttendanceRecords
            .Where(r => r.LocationId == request.LocationId && r.Date == today)
            .ToListAsync(cancellationToken);

        return records.Select(AttendanceMapper.ToResponse).ToList();
    }
}
