using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

// FR-002: identity resolved server-side from the JWT, same as ClockInCommand.
public record ClockOutCommand(Guid TenantUserId) : IRequest<StaffTimeEntryResult>;

public class ClockOutCommandHandler(ITenantDbContext db) : IRequestHandler<ClockOutCommand, StaffTimeEntryResult>
{
    public async Task<StaffTimeEntryResult> Handle(ClockOutCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.ProfileNotFound);

        var entry = await db.StaffTimeEntries.FirstOrDefaultAsync(
            e => e.StaffProfileId == profile.Id && e.ClockedOutAt == null, cancellationToken);
        if (entry is null)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.NoOpenEntry);

        var now = DateTime.UtcNow;
        entry.ClockedOutAt = now;
        entry.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return StaffTimeEntryResult.Success(StaffTimeEntryMapper.ToResponse(entry, now));
    }
}
