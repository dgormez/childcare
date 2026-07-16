using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Payments;

/// <summary>
/// Notifies every contact linked to a child when their invoice's betalingsbewijs becomes
/// available (spec.md FR-015) — invoked from both the online-payment webhook
/// (ProcessPaymentWebhookCommand) and 014's existing manual mark-paid command, so a receipt is
/// generated identically regardless of how the invoice reached Paid. Mirrors
/// InvoiceNotificationService's exact Notification row + best-effort push pattern, except the
/// tenant-scoped db context is passed into NotifyAsync explicitly rather than
/// constructor-injected: the webhook's TenantExempt route has no ambient tenant context
/// (research.md R2), so this service must accept whichever ITenantDbContext its caller already
/// resolved (either the normal DI-scoped one, or one built via ITenantDbContextResolver.ForSchema).
/// </summary>
public class PaymentReceiptNotificationService(
    IExpoPushSender pushSender,
    ILogger<PaymentReceiptNotificationService> logger)
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Betalingsbewijs beschikbaar", "Je betalingsbewijs staat klaar."),
        ["fr"] = ("Preuve de paiement disponible", "Votre preuve de paiement est disponible."),
        ["en"] = ("Receipt available", "Your payment receipt is ready."),
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
                    Type = NotificationType.InvoicePaid,
                    SourceId = invoice.Id,
                    TitleKey = "parent.notifications.invoice_paid.title",
                    BodyKey = "parent.notifications.invoice_paid.body",
                    ArgumentsJson = "{}",
                });
            }

            if (contact.PushToken is null)
            {
                logger.LogInformation("Invoice {Id} paid but contact {ContactId} has no push token.", invoice.Id, contact.Id);
                continue;
            }

            try
            {
                await pushSender.SendAsync(contact.PushToken, labels.Title, labels.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Payment-receipt push dispatch failed for invoice {Id}, contact {ContactId}.", invoice.Id, contact.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
