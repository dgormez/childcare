using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Messaging;

/// <summary>
/// Shared by both the parent route and the director/staff route (FR-004/FR-006) —
/// <paramref name="IsStaffOrDirector"/> is set by the endpoint per which route triggered it, not
/// re-derived here: a staff/director caller is authorized organisation-wide (no participant row
/// required), while a parent caller must already be a MessageThreadParticipant (FR-006).
/// </summary>
public record SendMessageCommand(Guid TenantUserId, Guid ThreadId, string Body, bool IsStaffOrDirector) : IRequest<SendMessageResult>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.message_thread.body_required")
            .MaximumLength(5000).WithMessage("errors.message_thread.body_too_long");
    }
}

public class SendMessageCommandHandler(
    ITenantDbContext db,
    IExpoPushSender pushSender,
    ILogger<SendMessageCommandHandler> logger) : IRequestHandler<SendMessageCommand, SendMessageResult>
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Nieuw bericht", "Je hebt een nieuw bericht van de opvang ontvangen."),
        ["fr"] = ("Nouveau message", "Vous avez reçu un nouveau message de la crèche."),
        ["en"] = ("New message", "You've received a new message from the KDV."),
    };

    public async Task<SendMessageResult> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var thread = await db.MessageThreads.FirstOrDefaultAsync(t => t.Id == request.ThreadId, cancellationToken);
        if (thread is null)
            return SendMessageResult.Fail(MessagingFailure.ThreadNotFound);

        if (!request.IsStaffOrDirector)
        {
            var isParticipant = await db.MessageThreadParticipants
                .AnyAsync(p => p.ThreadId == request.ThreadId && p.TenantUserId == request.TenantUserId, cancellationToken);
            if (!isParticipant)
                return SendMessageResult.Fail(MessagingFailure.NotParticipant);
        }

        var now = DateTime.UtcNow;

        // research.md R7: a single "read by the other side" marker. A staff/director-authored
        // message is already implicitly "read" by the KDV side (org-wide visibility means there
        // is no separate staff read-state to track); only a parent-authored message needs the
        // opposite-side ReadAt left null until a director/staff actually opens it.
        var message = new Message
        {
            ThreadId = request.ThreadId,
            SenderId = request.TenantUserId,
            Body = request.Body,
            SentAt = now,
        };
        db.Messages.Add(message);
        thread.LastActivityAt = now;

        List<MessageThreadParticipant> parentParticipants = [];
        if (request.IsStaffOrDirector)
        {
            parentParticipants = await db.MessageThreadParticipants
                .Where(p => p.ThreadId == request.ThreadId)
                .ToListAsync(cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        // FR-012/FR-015: notify every parent participant when staff/director sends a reply.
        // The sender themself (a parent replying) never gets a notification for their own
        // message.
        if (request.IsStaffOrDirector)
        {
            foreach (var participant in parentParticipants)
                await NotifyParentAsync(participant.TenantUserId, thread.Id, cancellationToken);
        }

        var sender = await db.Users.FirstAsync(u => u.Id == request.TenantUserId, cancellationToken);
        return SendMessageResult.Success(new MessageResponse(
            message.Id, message.ThreadId, message.SenderId, sender.Name, message.Body, message.SentAt, message.ReadAt));
    }

    private async Task NotifyParentAsync(Guid parentTenantUserId, Guid threadId, CancellationToken cancellationToken)
    {
        var parentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == parentTenantUserId, cancellationToken);
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.TenantUserId == parentTenantUserId, cancellationToken);
        if (parentUser is null || contact is null)
            return;

        db.Notifications.Add(new Notification
        {
            TenantUserId = parentTenantUserId,
            Type = NotificationType.NewMessage,
            SourceId = threadId,
            TitleKey = "parent.notifications.new_message.title",
            BodyKey = "parent.notifications.new_message.body",
        });
        await db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(contact.PushToken))
            return;

        var labels = Labels.TryGetValue(contact.Locale, out var localized) ? localized : Labels["nl"];
        try
        {
            await pushSender.SendAsync(contact.PushToken, labels.Title, labels.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            // FR-015: a push failure must never fail the send or block the in-app notification
            // already saved above.
            logger.LogWarning(ex, "New-message push notification dispatch failed for thread {ThreadId}.", threadId);
        }
    }
}
