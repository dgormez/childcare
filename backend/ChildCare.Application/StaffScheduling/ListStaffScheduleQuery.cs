using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

// One location's week (weekStart Monday through the following Sunday) — the rota builder's
// primary read (contracts/staff-schedules-api.md).
public record ListStaffScheduleQuery(Guid LocationId, DateOnly WeekStart) : IRequest<ListStaffScheduleResult>;

public class ListStaffScheduleQueryHandler(ITenantDbContext db) : IRequestHandler<ListStaffScheduleQuery, ListStaffScheduleResult>
{
    public async Task<ListStaffScheduleResult> Handle(ListStaffScheduleQuery request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId, cancellationToken);
        if (!locationExists)
            return ListStaffScheduleResult.Fail(StaffScheduleFailure.LocationNotFound);

        var weekEnd = request.WeekStart.AddDays(6);
        var entries = await db.StaffSchedules
            .Where(s => s.LocationId == request.LocationId && s.Date >= request.WeekStart && s.Date <= weekEnd)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return ListStaffScheduleResult.Success(entries.Select(StaffScheduleMapper.ToResponse).ToList());
    }
}
