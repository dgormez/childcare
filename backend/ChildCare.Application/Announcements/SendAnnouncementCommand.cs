using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Announcements;

public record SendAnnouncementCommand(Guid SentByTenantUserId, Guid LocationId, Guid? GroupId, string Subject, string Body) : IRequest<AnnouncementResult>;

public class SendAnnouncementCommandValidator : AbstractValidator<SendAnnouncementCommand>
{
    public SendAnnouncementCommandValidator(ITenantDbContext db)
    {
        RuleFor(x => x.Subject)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.announcement.subject_required")
            .MaximumLength(200).WithMessage("errors.announcement.subject_too_long");

        RuleFor(x => x.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.announcement.body_required")
            .MaximumLength(5000).WithMessage("errors.announcement.body_too_long");

        RuleFor(x => x.LocationId)
            .MustAsync(async (locationId, ct) => await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            .WithMessage("errors.location.not_found");

        RuleFor(x => x)
            .MustAsync(async (command, ct) => command.GroupId is null || await db.Groups.AnyAsync(g => g.Id == command.GroupId && g.LocationId == command.LocationId, ct))
            .WithMessage("errors.group.not_found");
    }
}

/// <summary>
/// FR-008: reach is bounded to contacts with an active parent account (research.md R8) — a
/// contact who was never invited has no notification centre or push token to reach; that is
/// this feature's own reachability boundary, not a gap this command introduces. FR-007 Scenario
/// 4: a scope with zero currently-enrolled children completes with zero recipients, not an error.
/// </summary>
public class SendAnnouncementCommandHandler(
    ITenantDbContext db,
    IExpoPushSender pushSender,
    ILogger<SendAnnouncementCommandHandler> logger) : IRequestHandler<SendAnnouncementCommand, AnnouncementResult>
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Nieuwe mededeling", "Er is een nieuwe mededeling van de opvang."),
        ["fr"] = ("Nouvelle annonce", "Il y a une nouvelle annonce de la crèche."),
        ["en"] = ("New announcement", "There's a new announcement from the KDV."),
    };

    public async Task<AnnouncementResult> Handle(SendAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var announcement = new Announcement
        {
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            Subject = request.Subject,
            Body = request.Body,
            SentByTenantUserId = request.SentByTenantUserId,
        };
        db.Announcements.Add(announcement);

        var recipientQuery = db.ChildGroupAssignments
            .Where(a => a.EndDate == null)
            .Join(db.Children, a => a.ChildId, c => c.Id, (a, c) => new { a.ChildId, a.GroupId, c.DeactivatedAt });

        // Scope: a specific group, or every currently-active group at the location.
        List<Guid> childIds;
        if (request.GroupId is Guid groupId)
        {
            childIds = await recipientQuery
                .Where(x => x.GroupId == groupId && x.DeactivatedAt == null)
                .Select(x => x.ChildId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
        else
        {
            var locationGroupIds = await db.Groups.Where(g => g.LocationId == request.LocationId).Select(g => g.Id).ToListAsync(cancellationToken);
            childIds = await recipientQuery
                .Where(x => locationGroupIds.Contains(x.GroupId) && x.DeactivatedAt == null)
                .Select(x => x.ChildId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        // FR-008: only contacts with an active parent account (TenantUserId != null) are reachable.
        var recipientContacts = childIds.Count == 0
            ? []
            : await db.ChildContacts
                .Where(cc => childIds.Contains(cc.ChildId))
                .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
                .Where(c => c.TenantUserId != null)
                .Distinct()
                .ToListAsync(cancellationToken);

        foreach (var contact in recipientContacts)
        {
            db.AnnouncementRecipients.Add(new AnnouncementRecipient { AnnouncementId = announcement.Id, ContactId = contact.Id });
            db.Notifications.Add(new Notification
            {
                TenantUserId = contact.TenantUserId!.Value,
                Type = NotificationType.Announcement,
                SourceId = announcement.Id,
                TitleKey = "parent.notifications.announcement.title",
                BodyKey = "parent.notifications.announcement.body",
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var contact in recipientContacts)
        {
            if (string.IsNullOrWhiteSpace(contact.PushToken))
                continue;

            var labels = Labels.TryGetValue(contact.Locale, out var localized) ? localized : Labels["nl"];
            try
            {
                await pushSender.SendAsync(contact.PushToken, labels.Title, labels.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Announcement push notification dispatch failed for announcement {AnnouncementId}.", announcement.Id);
            }
        }

        return AnnouncementResult.Success(new AnnouncementResponse(
            announcement.Id, announcement.LocationId, announcement.GroupId, announcement.Subject, announcement.Body,
            announcement.SentByTenantUserId, announcement.SentAt, recipientContacts.Count));
    }
}
