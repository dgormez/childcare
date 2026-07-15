using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Invoices;

/// <summary>
/// Notifies every contact linked to a child when their invoice is sent or re-sent after a
/// regenerate (spec.md FR-007/FR-011). Mirrors DayReservationNotificationService's in-app
/// Notification row + best-effort push pattern, generalized to every linked contact (not a
/// single requester) since an invoice has no single "requesting" contact — matches
/// ClosureParentRecipientResolver's "every contact linked to the child" resolution shape.
/// </summary>
public class InvoiceNotificationService(
    ITenantDbContext db,
    IExpoPushSender pushSender,
    ILogger<InvoiceNotificationService> logger)
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Nieuwe factuur", "Er staat een nieuwe factuur voor je klaar."),
        ["fr"] = ("Nouvelle facture", "Une nouvelle facture est disponible pour vous."),
        ["en"] = ("New invoice", "A new invoice is ready for you."),
    };

    public async Task NotifyAsync(Invoice invoice, CancellationToken cancellationToken = default)
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
                    Type = NotificationType.InvoiceSent,
                    SourceId = invoice.Id,
                    TitleKey = "parent.notifications.invoice_sent.title",
                    BodyKey = "parent.notifications.invoice_sent.body",
                    ArgumentsJson = "{}",
                });
            }

            if (contact.PushToken is null)
            {
                logger.LogInformation("Invoice {Id} sent but contact {ContactId} has no push token.", invoice.Id, contact.Id);
                continue;
            }

            try
            {
                await pushSender.SendAsync(contact.PushToken, labels.Title, labels.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Invoice-sent push dispatch failed for invoice {Id}, contact {ContactId}.", invoice.Id, contact.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
