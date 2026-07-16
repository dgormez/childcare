using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Payments;

/// <summary>
/// Notifies every contact linked to a child of an overdue invoice reminder (spec.md FR-014).
/// Mirrors InvoiceNotificationService's exact Notification row + best-effort push pattern, with
/// dedicated copy distinct from the "invoice sent" notification so a parent doesn't miss the
/// escalation. The tenant-scoped db context is passed into NotifyAsync explicitly rather than
/// constructor-injected — the reminder CLI job iterates every tenant schema via
/// ITenantDbContextResolver.ForSchema, outside any single request's ambient tenant context
/// (research.md R4/R2).
/// </summary>
public class PaymentReminderNotificationService(
    IExpoPushSender pushSender,
    ILogger<PaymentReminderNotificationService> logger)
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Factuur nog niet betaald", "Je hebt nog een openstaande factuur."),
        ["fr"] = ("Facture non payée", "Vous avez une facture impayée."),
        ["en"] = ("Invoice still unpaid", "You have an outstanding invoice."),
    };

    public async Task NotifyAsync(ITenantDbContext db, Invoice invoice, CancellationToken cancellationToken = default)
    {
        var contacts = await db.ChildContacts
            .Where(cc => cc.ChildId == invoice.ChildId)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .ToListAsync(cancellationToken);

        foreach (var contact in contacts)
        {
            var labels = Labels.TryGetValue(contact.Locale, out var found) ? found : Labels["nl"];

            if (contact.TenantUserId is not null)
            {
                db.Notifications.Add(new Notification
                {
                    TenantUserId = contact.TenantUserId.Value,
                    Type = NotificationType.PaymentReminder,
                    SourceId = invoice.Id,
                    TitleKey = "parent.notifications.payment_reminder.title",
                    BodyKey = "parent.notifications.payment_reminder.body",
                    ArgumentsJson = "{}",
                });
            }

            if (contact.PushToken is null)
            {
                logger.LogInformation("Reminder due for invoice {Id} but contact {ContactId} has no push token.", invoice.Id, contact.Id);
                continue;
            }

            try
            {
                await pushSender.SendAsync(contact.PushToken, labels.Title, labels.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Payment-reminder push dispatch failed for invoice {Id}, contact {ContactId}.", invoice.Id, contact.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
