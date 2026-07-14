using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.MealPreferenceRequests;

/// <summary>
/// Notifies the requesting parent when a director decides their meal-preference-change request
/// (research.md R3). Mirrors DayReservationNotificationService's exact shape: an in-app
/// Notification row for any linked account, a locale-rendered push attempt gated on having a
/// token, failures logged not thrown.
/// </summary>
public class MealPreferenceRequestNotificationService(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IExpoPushSender pushSender,
    ILogger<MealPreferenceRequestNotificationService> logger)
{
    private static readonly Dictionary<string, (string ApprovedTitle, string ApprovedBody, string RejectedTitle, string RejectedBody)> Labels = new()
    {
        ["nl"] = ("Voorkeur aangepast", "Je aanvraag is goedgekeurd.", "Aanvraag afgewezen", "Je aanvraag is afgewezen."),
        ["fr"] = ("Préférence modifiée", "Votre demande a été approuvée.", "Demande refusée", "Votre demande a été refusée."),
        ["en"] = ("Preference updated", "Your request has been approved.", "Request rejected", "Your request has been rejected."),
    };

    public async Task NotifyDecisionAsync(MealPreferenceChangeRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await contactResolver.ResolveAsync(request.RequestedBy, cancellationToken);
        if (contact is null)
        {
            logger.LogInformation("Meal preference request {Id} decided but requesting contact could not be resolved.", request.Id);
            return;
        }

        var approved = request.Status == MealPreferenceChangeRequestStatus.Approved;
        var labels = Labels.TryGetValue(contact.Locale, out var found) ? found : Labels["nl"];
        var resolvedTitle = approved ? labels.ApprovedTitle : labels.RejectedTitle;
        var resolvedBody = approved ? labels.ApprovedBody : labels.RejectedBody;
        if (!approved && !string.IsNullOrWhiteSpace(request.DecisionNotes))
            resolvedBody = $"{resolvedBody} {request.DecisionNotes}";

        if (contact.TenantUserId is not null)
        {
            // A null/blank DecisionNotes must never reach the client's i18n interpolation (would
            // render the literal string "null") — distinct bodyKey with no placeholder instead,
            // mirroring DayReservationNotificationService's identical precaution.
            var hasNote = !approved && !string.IsNullOrWhiteSpace(request.DecisionNotes);
            db.Notifications.Add(new Notification
            {
                TenantUserId = contact.TenantUserId.Value,
                Type = NotificationType.MealPreferenceRequestDecided,
                SourceId = request.Id,
                TitleKey = approved
                    ? "parent.notifications.meal_preference_request_decided.approved_title"
                    : "parent.notifications.meal_preference_request_decided.rejected_title",
                BodyKey = approved
                    ? "parent.notifications.meal_preference_request_decided.approved_body"
                    : hasNote
                        ? "parent.notifications.meal_preference_request_decided.rejected_body_with_note"
                        : "parent.notifications.meal_preference_request_decided.rejected_body",
                ArgumentsJson = hasNote
                    ? System.Text.Json.JsonSerializer.Serialize(new { decisionNotes = request.DecisionNotes })
                    : "{}",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (contact.PushToken is null)
        {
            logger.LogInformation("Meal preference request {Id} decided but requesting contact has no push token.", request.Id);
            return;
        }

        try
        {
            await pushSender.SendAsync(contact.PushToken, resolvedTitle, resolvedBody, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Meal preference request decision push dispatch failed for request {Id}.", request.Id);
        }
    }
}
