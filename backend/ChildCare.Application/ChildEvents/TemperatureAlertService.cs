using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.ChildEvents;

/// <summary>
/// All notification text is resolved from each recipient's own Contact.Locale — a push
/// notification has no client left to resolve an i18n key against, unlike every other
/// user-facing string in this codebase (constitution Principle IV), so it must arrive
/// pre-rendered. Mirrors QuestPdfContractGenerator's locale-dictionary pattern (the other place
/// this backend renders locale-aware text server-side).
/// </summary>
public class TemperatureAlertService(ITenantDbContext db, IExpoPushSender pushSender, ILogger<TemperatureAlertService> logger)
    : ITemperatureAlertService
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Verhoging gemeld", "Er is een verhoogde temperatuur van {0}°C gemeten."),
        ["fr"] = ("Fièvre signalée", "Une température élevée de {0}°C a été mesurée."),
        ["en"] = ("Fever reported", "A raised temperature of {0}°C has been recorded."),
    };

    public async Task NotifyAsync(Guid childId, decimal celsius, CancellationToken cancellationToken = default)
    {
        // FR-010: every ChildContact with CanPickup = true and a registered push token.
        var recipients = await db.ChildContacts
            .Where(cc => cc.ChildId == childId && cc.CanPickup)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => new { c.PushToken, c.Locale })
            .Where(r => r.PushToken != null)
            .ToListAsync(cancellationToken);

        // FR-011: zero eligible/registered recipients — log and continue, never fail the save.
        if (recipients.Count == 0)
        {
            logger.LogInformation(
                "Temperature alert for child {ChildId} ({Celsius}°C) had no deliverable recipients.", childId, celsius);
            return;
        }

        foreach (var recipient in recipients)
        {
            var (title, bodyTemplate) = Labels.TryGetValue(recipient.Locale, out var labels) ? labels : Labels["nl"];
            try
            {
                await pushSender.SendAsync(recipient.PushToken!, title, string.Format(bodyTemplate, celsius), cancellationToken);
            }
            catch (Exception ex)
            {
                // FR-011a: a transport-level dispatch failure never fails the event save, and
                // never blocks notifying the remaining recipients.
                logger.LogWarning(ex, "Temperature alert dispatch failed for child {ChildId}.", childId);
            }
        }
    }
}
