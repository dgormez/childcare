using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

public record RelockStaffTimeEntryCommand(Guid Id) : IRequest<StaffTimeEntryResult>;

public class RelockStaffTimeEntryCommandHandler(ITenantDbContext db) : IRequestHandler<RelockStaffTimeEntryCommand, StaffTimeEntryResult>
{
    public async Task<StaffTimeEntryResult> Handle(RelockStaffTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffTimeEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.NotFound);

        var now = DateTime.UtcNow;
        entry.UnlockedAt = null;
        entry.UnlockedBy = null;
        entry.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return StaffTimeEntryResult.Success(StaffTimeEntryMapper.ToResponse(entry, now));
    }
}
