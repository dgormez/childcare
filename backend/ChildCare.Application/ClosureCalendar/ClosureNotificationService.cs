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
        var targetRecipients = await recipients.ResolveAsync(closure.LocationId, closure.Date, cancellationToken);
        var pushSent = 0;
        var pushFailed = 0;
        var messagesCreated = 0;

        foreach (var recipient in targetRecipients)
        {
            var exists = await db.ClosureNotificationDeliveries.AnyAsync(
                d => d.ClosureDayId == closure.Id && d.ContactId == recipient.ContactId && d.Kind == kind,
                cancellationToken);
            if (exists)
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
                    : ClosureDeliveryStatus.Sent,
            };

            if (!string.IsNullOrWhiteSpace(recipient.PushToken))
            {
                var labels = Labels.TryGetValue(recipient.Locale, out var localized) ? localized : Labels["nl"];
                var title = kind == ClosureNotificationKind.Published ? labels.PublishedTitle : labels.CancelledTitle;
                var body = kind == ClosureNotificationKind.Published
                    ? string.Format(labels.PublishedBody, location.Name, closure.Date, closure.Label)
                    : string.Format(labels.CancelledBody, location.Name, closure.Date);
                try
                {
                    await pushSender.SendAsync(recipient.PushToken, title, body, cancellationToken);
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

            db.ClosureNotificationDeliveries.Add(delivery);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ClosureNotificationSummary(targetRecipients.Count, pushSent, pushFailed, messagesCreated);
    }
}
