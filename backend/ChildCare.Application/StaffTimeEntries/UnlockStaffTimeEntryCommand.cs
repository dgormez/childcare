using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

// FR-007/FR-007a: unlocking is attributable (UnlockedBy) and does not expire on its own — a
// director must explicitly re-lock (RelockStaffTimeEntryCommand).
public record UnlockStaffTimeEntryCommand(Guid Id, Guid DirectorTenantUserId) : IRequest<StaffTimeEntryResult>;

public class UnlockStaffTimeEntryCommandHandler(ITenantDbContext db) : IRequestHandler<UnlockStaffTimeEntryCommand, StaffTimeEntryResult>
{
    public async Task<StaffTimeEntryResult> Handle(UnlockStaffTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffTimeEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.NotFound);

        var now = DateTime.UtcNow;
        entry.UnlockedAt = now;
        entry.UnlockedBy = request.DirectorTenantUserId;
        entry.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return StaffTimeEntryResult.Success(StaffTimeEntryMapper.ToResponse(entry, now));
    }
}
