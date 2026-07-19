using System.Text.Json;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.ClosureCalendar;

public record ClosureNotificationSummary(int Recipients, int PushSent, int PushFailed, int MessagesCreated);

public class ClosureNotificationService(
    ITenantDbContext db,
    IExpoPushSender pushSender,
    IEmailSender emailSender,
    ClosureParentRecipientResolver recipients,
    ILogger<ClosureNotificationService> logger)
{
    private static readonly Dictionary<string, (string PublishedTitle, string PublishedBody, string CancelledTitle, string CancelledBody)> Labels = new()
    {
        ["nl"] = ("Sluitingsdag", "{0} is gesloten op {1}: {2}.", "Sluiting geannuleerd", "De sluiting van {0} op {1} is geannuleerd."),
        ["fr"] = ("Jour de fermeture", "{0} est fermé le {1} : {2}.", "Fermeture annulée", "La fermeture de {0} le {1} est annulée."),
        ["en"] = ("Closure day", "{0} is closed on {1}: {2}.", "Closure cancelled", "The closure of {0} on {1} has been cancelled."),
    };

    public async Task<ClosureNotificationSummary> NotifyAsync(
        KdvClosureDay closure,
        ClosureNotificationKind kind,
        CancellationToken cancellationToken = default)
    {
        var location = await db.Locations.FirstAsync(l => l.Id == closure.LocationId, cancellationToken);
        var targetRecipients = await ResolveRecipientsAsync(closure, kind, cancellationToken);
        var existingContactIds = await db.ClosureNotificationDeliveries
            .Where(d => d.ClosureDayId == closure.Id && d.Kind == kind)
            .Select(d => d.ContactId)
            .ToListAsync(cancellationToken);
        var alreadyDelivered = existingContactIds.ToHashSet();
        var messagesCreated = 0;
        var deliveriesToSend = new List<(ClosureParentRecipient Recipient, ClosureNotificationDelivery Delivery)>();
        // FR-010: emails every resolved contact with an address on file, independent of
        // TenantUserId/push-token state — reaches a contact with no parent-app account at all,
        // which the push-only fan-out above can never reach (research.md R4, no TenantUserId
        // gate). Gated by the same alreadyDelivered idempotency check as push, so a retried
        // NotifyAsync call doesn't re-email a contact already notified for this closure+kind.
        var emailRecipients = new List<ClosureParentRecipient>();

        foreach (var recipient in targetRecipients)
        {
            if (alreadyDelivered.Contains(recipient.ContactId))
                continue;

            var message = new ParentClosureMessage
            {
                ContactId = recipient.ContactId,
                ClosureDayId = closure.Id,
                Kind = kind,
                TitleKey = kind == ClosureNotificationKind.Published
                    ? "parent.closures.published.title"
                    : "parent.closures.cancelled.title",
                BodyKey = kind == ClosureNotificationKind.Published
                    ? "parent.closures.published.body"
                    : "parent.closures.cancelled.body",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    locationName = location.Name,
                    date = closure.Date,
                    label = closure.Label,
                    closureType = ClosureCalendarMapper.ToWire(closure.ClosureType),
                }),
            };
            db.ParentClosureMessages.Add(message);
            messagesCreated++;

            var delivery = new ClosureNotificationDelivery
            {
                ClosureDayId = closure.Id,
                ContactId = recipient.ContactId,
                Kind = kind,
                PushToken = recipient.PushToken,
                MessageId = message.Id,
                PushStatus = string.IsNullOrWhiteSpace(recipient.PushToken)
                    ? ClosureDeliveryStatus.NotApplicable
                    : ClosureDeliveryStatus.Pending,
            };

            db.ClosureNotificationDeliveries.Add(delivery);
            if (!string.IsNullOrWhiteSpace(recipient.PushToken))
                deliveriesToSend.Add((recipient, delivery));
            if (!string.IsNullOrWhiteSpace(recipient.Email))
                emailRecipients.Add(recipient);
        }

        await db.SaveChangesAsync(cancellationToken);

        var pushSent = 0;
        var pushFailed = 0;
        foreach (var (recipient, delivery) in deliveriesToSend)
        {
            var labels = Labels.TryGetValue(recipient.Locale, out var localized) ? localized : Labels["nl"];
            var title = kind == ClosureNotificationKind.Published ? labels.PublishedTitle : labels.CancelledTitle;
            var body = kind == ClosureNotificationKind.Published
                ? string.Format(labels.PublishedBody, location.Name, closure.Date, closure.Label)
                : string.Format(labels.CancelledBody, location.Name, closure.Date);
            try
            {
                await pushSender.SendAsync(recipient.PushToken!, title, body, cancellationToken);
                delivery.PushStatus = ClosureDeliveryStatus.Sent;
                pushSent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Closure notification dispatch failed for closure {ClosureDayId}.", closure.Id);
                delivery.PushStatus = ClosureDeliveryStatus.Failed;
                delivery.Error = ex.GetType().Name;
                pushFailed++;
            }
        }

        if (deliveriesToSend.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        foreach (var recipient in emailRecipients)
        {
            var labels = Labels.TryGetValue(recipient.Locale, out var localized) ? localized : Labels["nl"];
            var title = kind == ClosureNotificationKind.Published ? labels.PublishedTitle : labels.CancelledTitle;
            var body = kind == ClosureNotificationKind.Published
                ? string.Format(labels.PublishedBody, location.Name, closure.Date, closure.Label)
                : string.Format(labels.CancelledBody, location.Name, closure.Date);
            try
            {
                await emailSender.SendClosureNotificationEmailAsync(recipient.Email!, recipient.Locale, title, body, cancellationToken);
            }
            catch (Exception ex)
            {
                // FR-012: a bad/bounced address doesn't block the rest of the batch — logged
                // server-side only, matching CLAUDE.md's error-handling rule.
                logger.LogWarning(ex, "Closure notification email dispatch failed for closure {ClosureDayId}.", closure.Id);
            }
        }

        return new ClosureNotificationSummary(targetRecipients.Count, pushSent, pushFailed, messagesCreated);
    }

    private async Task<IReadOnlyList<ClosureParentRecipient>> ResolveRecipientsAsync(
        KdvClosureDay closure,
        ClosureNotificationKind kind,
        CancellationToken cancellationToken)
    {
        if (kind == ClosureNotificationKind.Published)
            return await recipients.ResolveAsync(closure.LocationId, closure.Date, cancellationToken);

        var publishedContacts = await db.ClosureNotificationDeliveries
            .Where(d => d.ClosureDayId == closure.Id && d.Kind == ClosureNotificationKind.Published)
            .Join(
                db.Contacts,
                d => d.ContactId,
                c => c.Id,
                (d, c) => new ClosureParentRecipient(c.Id, c.Locale, c.PushToken ?? d.PushToken, c.Email))
            .ToListAsync(cancellationToken);

        return publishedContacts
            .GroupBy(r => r.ContactId)
            .Select(g => g.First())
            .ToList();
    }
}
