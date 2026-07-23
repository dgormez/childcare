using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

// FR-001 Acceptance Scenario 3: staff-mobile needs to know its own open-entry state on app
// load/reopen, not just from a clock-in/out response — identity resolved from the JWT, same as
// every other self-service query in this module.
public record GetMyOpenTimeEntryQuery(Guid TenantUserId) : IRequest<StaffTimeEntryResponse?>;

public class GetMyOpenTimeEntryQueryHandler(ITenantDbContext db) : IRequestHandler<GetMyOpenTimeEntryQuery, StaffTimeEntryResponse?>
{
    public async Task<StaffTimeEntryResponse?> Handle(GetMyOpenTimeEntryQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return null;

        var entry = await db.StaffTimeEntries.FirstOrDefaultAsync(
            e => e.StaffProfileId == profile.Id && e.ClockedOutAt == null, cancellationToken);

        return entry is null ? null : StaffTimeEntryMapper.ToResponse(entry, DateTime.UtcNow);
    }
}
