using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Messaging;

/// <summary>
/// Shared by the parent route and the director/staff route (mirrors SendMessageCommand's
/// IsStaffOrDirector split). Marks the appropriate side's unread messages read on fetch
/// (research.md R7): a parent viewing marks staff-authored messages read; a director/staff
/// viewing marks parent-authored messages read.
/// </summary>
public record GetThreadQuery(Guid TenantUserId, Guid ThreadId, bool IsStaffOrDirector) : IRequest<MessageThreadResult>;

public class GetThreadQueryHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<GetThreadQuery, MessageThreadResult>
{
    public async Task<MessageThreadResult> Handle(GetThreadQuery request, CancellationToken cancellationToken)
    {
        var thread = await db.MessageThreads.FirstOrDefaultAsync(t => t.Id == request.ThreadId, cancellationToken);
        if (thread is null)
            return MessageThreadResult.Fail(MessagingFailure.ThreadNotFound);

        if (!request.IsStaffOrDirector)
        {
            var isParticipant = await db.MessageThreadParticipants
                .AnyAsync(p => p.ThreadId == request.ThreadId && p.TenantUserId == request.TenantUserId, cancellationToken);
            if (!isParticipant)
                return MessageThreadResult.Fail(MessagingFailure.NotParticipant);
        }

        var parentUserIds = await db.Users.Where(u => u.Role == UserRole.Parent).Select(u => u.Id).ToListAsync(cancellationToken);

        var messages = await db.Messages
            .Where(m => m.ThreadId == request.ThreadId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);

        var unreadToMark = request.IsStaffOrDirector
            ? messages.Where(m => parentUserIds.Contains(m.SenderId) && m.ReadAt is null)
            : messages.Where(m => !parentUserIds.Contains(m.SenderId) && m.ReadAt is null);

        var now = DateTime.UtcNow;
        var anyMarked = false;
        foreach (var message in unreadToMark)
        {
            message.ReadAt = now;
            anyMarked = true;
        }
        if (anyMarked)
            await db.SaveChangesAsync(cancellationToken);

        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var senders = await db.Users.Where(u => senderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);

        var messageResponses = messages
            .Select(m => new MessageResponse(m.Id, m.ThreadId, m.SenderId, senders.TryGetValue(m.SenderId, out var s) ? s.Name : "", m.Body, m.SentAt, m.ReadAt))
            .ToList();

        string? childName = null;
        if (thread.ChildId is Guid childId)
        {
            var child = await db.Children.FirstOrDefaultAsync(c => c.Id == childId, cancellationToken);
            if (child is not null)
                childName = $"{child.FirstName} {child.LastName}";
        }

        var hasUnread = messages.Any(m => m.ReadAt is null && m.SenderId != request.TenantUserId);

        return MessageThreadResult.Success(new MessageThreadResponse(
            thread.Id, thread.Subject, thread.ChildId, childName, thread.CreatedAt, thread.LastActivityAt, hasUnread, messageResponses));
    }
}
