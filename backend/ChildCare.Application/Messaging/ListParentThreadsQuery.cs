using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Messaging;

public record ListParentThreadsQuery(Guid TenantUserId) : IRequest<IReadOnlyList<MessageThreadSummaryResponse>>;

public class ListParentThreadsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListParentThreadsQuery, IReadOnlyList<MessageThreadSummaryResponse>>
{
    public async Task<IReadOnlyList<MessageThreadSummaryResponse>> Handle(ListParentThreadsQuery request, CancellationToken cancellationToken)
    {
        var threadIds = await db.MessageThreadParticipants
            .Where(p => p.TenantUserId == request.TenantUserId)
            .Select(p => p.ThreadId)
            .ToListAsync(cancellationToken);

        var threads = await db.MessageThreads
            .Where(t => threadIds.Contains(t.Id))
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync(cancellationToken);

        var children = await db.Children
            .Where(c => threads.Select(t => t.ChildId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var results = new List<MessageThreadSummaryResponse>(threads.Count);
        foreach (var thread in threads)
        {
            // A message is unread by this parent when it wasn't sent by them and ReadAt is null
            // (research.md R7 — the shared cross-side marker).
            var hasUnread = await db.Messages.AnyAsync(
                m => m.ThreadId == thread.Id && m.SenderId != request.TenantUserId && m.ReadAt == null, cancellationToken);

            var childName = thread.ChildId is Guid childId && children.TryGetValue(childId, out var child)
                ? $"{child.FirstName} {child.LastName}"
                : null;

            results.Add(new MessageThreadSummaryResponse(thread.Id, thread.Subject, thread.ChildId, childName, thread.LastActivityAt, hasUnread, 0));
        }

        return results;
    }
}
