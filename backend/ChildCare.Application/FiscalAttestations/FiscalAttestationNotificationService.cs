using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.FiscalAttestations;

/// <summary>
/// Notifies every contact linked to a child when their fiscal attestation is generated or
/// regenerated (spec.md FR-016, Clarifications 2026-07-16). Mirrors InvoiceNotificationService's
/// exact Notification row + best-effort push pattern.
/// </summary>
public class FiscalAttestationNotificationService(
    ITenantDbContext db,
    IExpoPushSender pushSender,
    ILogger<FiscalAttestationNotificationService> logger)
{
    private static readonly Dictionary<string, (string Title, string Body)> Labels = new()
    {
        ["nl"] = ("Fiscaal attest klaar", "Je fiscaal attest staat klaar."),
        ["fr"] = ("Attestation fiscale prête", "Votre attestation fiscale est disponible."),
        ["en"] = ("Fiscal attestation ready", "Your fiscal attestation is ready."),
    };

    public async Task NotifyAsync(FiscalAttestation attestation, CancellationToken cancellationToken = default)
    {
        var contacts = await db.ChildContacts
            .Where(cc => cc.ChildId == attestation.ChildId)
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
                    Type = NotificationType.FiscalAttestationGenerated,
                    SourceId = attestation.Id,
                    TitleKey = "parent.notifications.fiscal_attestation_ready.title",
                    BodyKey = "parent.notifications.fiscal_attestation_ready.body",
                    ArgumentsJson = "{}",
                });
            }

            if (contact.PushToken is null)
            {
                logger.LogInformation("Fiscal attestation {Id} ready but contact {ContactId} has no push token.", attestation.Id, contact.Id);
                continue;
            }

            try
            {
                await pushSender.SendAsync(contact.PushToken, labels.Title, labels.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fiscal-attestation-ready push dispatch failed for attestation {Id}, contact {ContactId}.", attestation.Id, contact.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
