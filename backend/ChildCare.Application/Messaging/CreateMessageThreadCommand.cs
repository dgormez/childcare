using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Messaging;

public record CreateMessageThreadCommand(Guid TenantUserId, Guid? ChildId, string Subject, string Body) : IRequest<MessageThreadResult>;

public class CreateMessageThreadCommandValidator : AbstractValidator<CreateMessageThreadCommand>
{
    public CreateMessageThreadCommandValidator()
    {
        RuleFor(x => x.Subject)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.message_thread.subject_required")
            .MaximumLength(200).WithMessage("errors.message_thread.subject_too_long");

        RuleFor(x => x.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.message_thread.body_required")
            .MaximumLength(5000).WithMessage("errors.message_thread.body_too_long");
    }
}

/// <summary>
/// FR-003a: every parent contact of the child with an active parent account becomes a
/// participant on the same shared thread — a child's parents share one conversation with the
/// KDV, not one each (research.md R6).
/// </summary>
public class CreateMessageThreadCommandHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<CreateMessageThreadCommand, MessageThreadResult>
{
    public async Task<MessageThreadResult> Handle(CreateMessageThreadCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return MessageThreadResult.Fail(MessagingFailure.NotParticipant);

        if (request.ChildId is Guid childId)
        {
            var isContactOfChild = await db.ChildContacts.AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == childId, cancellationToken);
            if (!isContactOfChild)
                return MessageThreadResult.Fail(MessagingFailure.ChildNotFound);
        }

        var now = DateTime.UtcNow;
        var thread = new MessageThread
        {
            Subject = request.Subject,
            ChildId = request.ChildId,
            CreatedAt = now,
            LastActivityAt = now,
        };
        db.MessageThreads.Add(thread);

        var participantIds = new HashSet<Guid> { request.TenantUserId };
        if (request.ChildId is Guid cid)
        {
            var otherParentUserIds = await db.ChildContacts
                .Where(cc => cc.ChildId == cid)
                .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c.TenantUserId)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .ToListAsync(cancellationToken);
            foreach (var id in otherParentUserIds)
                participantIds.Add(id);
        }

        foreach (var participantId in participantIds)
        {
            db.MessageThreadParticipants.Add(new MessageThreadParticipant
            {
                ThreadId = thread.Id,
                TenantUserId = participantId,
                AddedAt = now,
            });
        }

        var message = new Message
        {
            ThreadId = thread.Id,
            SenderId = request.TenantUserId,
            Body = request.Body,
            SentAt = now,
        };
        db.Messages.Add(message);

        await db.SaveChangesAsync(cancellationToken);

        var sender = await db.Users.FirstAsync(u => u.Id == request.TenantUserId, cancellationToken);
        var response = new MessageThreadResponse(
            thread.Id, thread.Subject, thread.ChildId, null, thread.CreatedAt, thread.LastActivityAt, false,
            [new MessageResponse(message.Id, thread.Id, message.SenderId, sender.Name, message.Body, message.SentAt, message.ReadAt)]);

        return MessageThreadResult.Success(response);
    }
}
