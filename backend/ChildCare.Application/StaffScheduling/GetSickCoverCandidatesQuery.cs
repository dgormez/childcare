using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

// FR-006, research.md R5: eligible-cover candidates for the given absent staff member's
// assignment on the given date — reuses StaffLocationEligibility (same check create/update
// already use) and an overlap filter (same range predicate OverlapCheck.ExistsAsync shares),
// excluding deactivated staff (mirrors GetProjectedOnDutyQuery's exclusion pattern).
public record GetSickCoverCandidatesQuery(DateOnly Date, Guid ExcludeStaffProfileId) : IRequest<SickCoverCandidatesResult>;

public class GetSickCoverCandidatesQueryHandler(ITenantDbContext db) : IRequestHandler<GetSickCoverCandidatesQuery, SickCoverCandidatesResult>
{
    public async Task<SickCoverCandidatesResult> Handle(GetSickCoverCandidatesQuery request, CancellationToken cancellationToken)
    {
        var absentEntry = await db.StaffSchedules.FirstOrDefaultAsync(
            s => s.StaffProfileId == request.ExcludeStaffProfileId && s.Date == request.Date && s.Status == StaffScheduleStatus.Absent,
            cancellationToken);
        if (absentEntry is null)
            return SickCoverCandidatesResult.Fail(StaffScheduleFailure.NoAbsentAssignment);

        var eligibleProfiles = await db.StaffLocationEligibility
            .Where(e => e.LocationId == absentEntry.LocationId && e.StaffProfileId != request.ExcludeStaffProfileId)
            .Join(db.StaffProfiles, e => e.StaffProfileId, p => p.Id, (e, p) => p)
            .Where(p => p.DeactivatedAt == null)
            .ToListAsync(cancellationToken);

        var candidateIds = eligibleProfiles.Select(p => p.Id).ToList();
        var conflictingIds = await db.StaffSchedules
            .Where(s => candidateIds.Contains(s.StaffProfileId) && s.Date == request.Date
                        && s.StartTime < absentEntry.EndTime && absentEntry.StartTime < s.EndTime)
            .Select(s => s.StaffProfileId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var candidates = eligibleProfiles
            .Where(p => !conflictingIds.Contains(p.Id))
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new SickCoverCandidateResponse(p.Id, $"{p.FirstName} {p.LastName}", p.QualificationLevel?.ToString()))
            .ToList();

        return SickCoverCandidatesResult.Success(candidates);
    }
}
