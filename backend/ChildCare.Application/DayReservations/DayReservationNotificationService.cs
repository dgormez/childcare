using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.DayReservations;

/// <summary>
/// Notifies the requesting parent when a director decides their request (FR-013). Mirrors
/// TemperatureAlertService's pattern: an in-app Notification row for any linked account,
/// a locale-rendered push attempt gated on having a token, failures logged not thrown
/// (research.md R4 for both this and feature 009's precedent).
/// </summary>
public class DayReservationNotificationService(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IExpoPushSender pushSender,
    ILogger<DayReservationNotificationService> logger)
{
    private static readonly Dictionary<string, (string ApprovedTitle, string ApprovedBody, string RejectedTitle, string RejectedBody)> Labels = new()
    {
        ["nl"] = ("Aanvraag goedgekeurd", "Je aanvraag is goedgekeurd.", "Aanvraag afgewezen", "Je aanvraag is afgewezen."),
        ["fr"] = ("Demande approuvée", "Votre demande a été approuvée.", "Demande refusée", "Votre demande a été refusée."),
        ["en"] = ("Request approved", "Your request has been approved.", "Request rejected", "Your request has been rejected."),
    };

    public async Task NotifyDecisionAsync(DayReservation reservation, CancellationToken cancellationToken = default)
    {
        var contact = await contactResolver.ResolveAsync(reservation.RequestedBy, cancellationToken);
        if (contact is null)
        {
            logger.LogInformation("Day reservation {Id} decided but requesting contact could not be resolved.", reservation.Id);
            return;
        }

        var approved = reservation.Status == DayReservationStatus.Approved;
        var labels = Labels.TryGetValue(contact.Locale, out var found) ? found : Labels["nl"];
        var resolvedTitle = approved ? labels.ApprovedTitle : labels.RejectedTitle;
        var resolvedBody = approved ? labels.ApprovedBody : labels.RejectedBody;
        if (!approved && !string.IsNullOrWhiteSpace(reservation.DirectorNotes))
            resolvedBody = $"{resolvedBody} {reservation.DirectorNotes}";

        if (contact.TenantUserId is not null)
        {
            // A null/blank DirectorNotes must never reach the client's i18n interpolation (it
            // would render the literal string "null") — pick a distinct bodyKey with no
            // placeholder instead of passing an empty argument (mirrors TemperatureAlertService's
            // `celsius` argument precedent: the JSON property name is deliberately lowercase,
            // camelCase, to match the client-side interpolation placeholder exactly).
            var hasNote = !approved && !string.IsNullOrWhiteSpace(reservation.DirectorNotes);
            db.Notifications.Add(new Notification
            {
                TenantUserId = contact.TenantUserId.Value,
                Type = NotificationType.DayReservationDecided,
                SourceId = reservation.Id,
                TitleKey = approved
                    ? "parent.notifications.day_reservation_decided.approved_title"
                    : "parent.notifications.day_reservation_decided.rejected_title",
                BodyKey = approved
                    ? "parent.notifications.day_reservation_decided.approved_body"
                    : hasNote
                        ? "parent.notifications.day_reservation_decided.rejected_body_with_note"
                        : "parent.notifications.day_reservation_decided.rejected_body",
                ArgumentsJson = hasNote
                    ? System.Text.Json.JsonSerializer.Serialize(new { directorNotes = reservation.DirectorNotes })
                    : "{}",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (contact.PushToken is null)
        {
            logger.LogInformation("Day reservation {Id} decided but requesting contact has no push token.", reservation.Id);
            return;
        }

        try
        {
            await pushSender.SendAsync(contact.PushToken, resolvedTitle, resolvedBody, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Day reservation decision push dispatch failed for reservation {Id}.", reservation.Id);
        }
    }
}
