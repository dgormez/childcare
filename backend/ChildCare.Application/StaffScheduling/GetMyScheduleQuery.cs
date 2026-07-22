using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

/// <summary>
/// FR-012: self-service lookup for the caller's own schedule (from today forward), for a
/// future consumer (feature 027) — no UI ships in this feature. Resolves TenantUserId from
/// the JWT's ClaimTypes.NameIdentifier claim (extracted by the endpoint, not this query),
/// mirroring GetStaffMeQuery's precedent (feature 008), so a caregiver can never look up
/// anyone else's schedule through this query.
/// </summary>
public record GetMyScheduleQuery(Guid TenantUserId) : IRequest<GetMyScheduleResult>;

public record GetMyScheduleResult(bool Found, IReadOnlyList<StaffScheduleResponse> Entries);

public class GetMyScheduleQueryHandler(ITenantDbContext db) : IRequestHandler<GetMyScheduleQuery, GetMyScheduleResult>
{
    public async Task<GetMyScheduleResult> Handle(GetMyScheduleQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return new GetMyScheduleResult(false, []);

        var today = BelgianCalendarDay.Today();
        // FR-001/contracts/staff-app-api.md: only published rows are visible to their own staff
        // member — an unpublished (draft) week must never leak through this read.
        var entries = await db.StaffSchedules
            .Where(s => s.StaffProfileId == profile.Id && s.Date >= today && s.IsPublished)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return new GetMyScheduleResult(true, entries.Select(StaffScheduleMapper.ToResponse).ToList());
    }
}
