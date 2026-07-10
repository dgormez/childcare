using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

// FR-007: planning-only projected on-duty count derived from staff_schedules — mirrors
// GetBkrRatioQuery's qualification-exclusion rule (StudentVolunteer never counts) for
// consistency of meaning, but reads StaffSchedule instead of RoomShift and is never consulted
// by GetBkrRatioQuery itself (research.md R1). Also excludes absence (FR-006) and deactivated
// staff (FR-009b) — neither of which GetBkrRatioQuery needs, since a deactivated/absent staff
// member simply never checks in there.
public record GetProjectedOnDutyQuery(Guid LocationId, DateOnly Date, TimeOnly Time) : IRequest<ProjectedOnDutyResult>;

public class GetProjectedOnDutyQueryHandler(ITenantDbContext db) : IRequestHandler<GetProjectedOnDutyQuery, ProjectedOnDutyResult>
{
    public async Task<ProjectedOnDutyResult> Handle(GetProjectedOnDutyQuery request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId, cancellationToken);
        if (!locationExists)
            return ProjectedOnDutyResult.Fail(StaffScheduleFailure.LocationNotFound);

        var staffIds = await db.StaffSchedules
            .Where(s => s.LocationId == request.LocationId && s.Date == request.Date
                        && s.StartTime <= request.Time && request.Time < s.EndTime
                        && !s.IsAbsent)
            .Join(db.StaffProfiles, s => s.StaffProfileId, p => p.Id, (s, p) => p)
            .Where(p => p.QualificationLevel != QualificationLevel.StudentVolunteer && p.DeactivatedAt == null)
            .Select(p => p.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ProjectedOnDutyResult.Success(new ProjectedOnDutyResponse(staffIds.Count, staffIds));
    }
}
