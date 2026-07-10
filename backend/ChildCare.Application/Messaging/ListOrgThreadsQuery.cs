using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Messaging;

/// <summary>Director/staff-scoped, organisation-wide (FR-004) — every thread, not participant-gated.</summary>
public record ListOrgThreadsQuery : IRequest<IReadOnlyList<MessageThreadSummaryResponse>>;

public class ListOrgThreadsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListOrgThreadsQuery, IReadOnlyList<MessageThreadSummaryResponse>>
{
    public async Task<IReadOnlyList<MessageThreadSummaryResponse>> Handle(ListOrgThreadsQuery request, CancellationToken cancellationToken)
    {
        var threads = await db.MessageThreads
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync(cancellationToken);

        var children = await db.Children
            .Where(c => threads.Select(t => t.ChildId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        // FR-013: staff/director-facing unread indicator — a COUNT of parent-authored messages
        // not yet read by any director/staff, derived directly from Messages joined against
        // Users.Role (research.md R7), no separate staff-notification table.
        var parentUserIds = await db.Users
            .Where(u => u.Role == UserRole.Parent)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var results = new List<MessageThreadSummaryResponse>(threads.Count);
        foreach (var thread in threads)
        {
            var unreadFromParentCount = await db.Messages.CountAsync(
                m => m.ThreadId == thread.Id && parentUserIds.Contains(m.SenderId) && m.ReadAt == null, cancellationToken);

            var childName = thread.ChildId is Guid childId && children.TryGetValue(childId, out var child)
                ? $"{child.FirstName} {child.LastName}"
                : null;

            results.Add(new MessageThreadSummaryResponse(thread.Id, thread.Subject, thread.ChildId, childName, thread.LastActivityAt, unreadFromParentCount > 0, unreadFromParentCount));
        }

        return results;
    }
}
