using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

/// <summary>Direction is "up" or "down" (contracts/waiting-list-api.md).</summary>
public record ReorderWaitingListEntryCommand(Guid Id, string Direction) : IRequest<ReorderWaitingListResult>;

public class ReorderWaitingListEntryCommandValidator : AbstractValidator<ReorderWaitingListEntryCommand>
{
    public ReorderWaitingListEntryCommandValidator()
    {
        RuleFor(x => x.Direction).Must(d => d is "up" or "down");
    }
}

public class ReorderWaitingListEntryCommandHandler(ITenantDbContext db) : IRequestHandler<ReorderWaitingListEntryCommand, ReorderWaitingListResult>
{
    public async Task<ReorderWaitingListResult> Handle(ReorderWaitingListEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return ReorderWaitingListResult.Fail(WaitingListFailure.NotFound);

        // FR-005: reordering only applies to `waiting` entries — priority only encodes queue
        // position for entries still awaiting an offer (research.md R6).
        if (entry.Status != WaitingListStatus.Waiting)
            return ReorderWaitingListResult.Fail(WaitingListFailure.NotReorderableInCurrentStatus);

        // FR-006: scoped per location — only compares/swaps against the same location's queue.
        var queue = await db.WaitingListEntries
            .Where(e => e.LocationId == entry.LocationId && e.Status == WaitingListStatus.Waiting)
            .OrderBy(e => e.Priority)
            .ToListAsync(cancellationToken);

        var index = queue.FindIndex(e => e.Id == entry.Id);
        var neighborIndex = request.Direction == "up" ? index - 1 : index + 1;

        if (neighborIndex < 0 || neighborIndex >= queue.Count)
            return ReorderWaitingListResult.Fail(WaitingListFailure.AlreadyAtBoundary);

        var neighbor = queue[neighborIndex];
        (entry.Priority, neighbor.Priority) = (neighbor.Priority, entry.Priority);
        entry.UpdatedAt = DateTime.UtcNow;
        neighbor.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var entries = await WaitingListQueries.BuildFilteredList(db, entry.LocationId, status: null, cancellationToken);
        return ReorderWaitingListResult.Success(entries);
    }
}
