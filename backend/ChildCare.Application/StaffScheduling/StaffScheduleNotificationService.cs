using System.Text.Json;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.StaffScheduling;

/// <summary>
/// FR-008/FR-008a: push + in-app Notification for the three staff-facing events (publish,
/// assignment change/cover, — LeaveRequestDecided lives in
/// StaffLeaveRequestNotificationService, mirroring the StaffScheduling/StaffLeaveRequests folder
/// split) plus the director-facing sick-report alert (deviation — see NotificationType.cs's
/// StaffSickReported comment). Mirrors DayReservationNotificationService's shape (Notification
/// row for any linked account, a locale-rendered push attempt gated on having a token, failures
/// logged not thrown) but resolves recipients directly from StaffProfile (TenantUserId +
/// PushToken live on the same row here, unlike the parent side's Contact indirection) — and
/// defaults every render to "nl" since StaffProfile carries no locale field anywhere in this
/// codebase today (same fallback every existing Labels dictionary already uses when a locale is
/// missing/unrecognized).
/// </summary>
public class StaffScheduleNotificationService(ITenantDbContext db, IExpoPushSender pushSender, ILogger<StaffScheduleNotificationService> logger)
{
    private static readonly Dictionary<string, (string Title, string Body)> PublishedLabels = new()
    {
        ["nl"] = ("Rooster gepubliceerd", "Je rooster is bijgewerkt. Bekijk je nieuwe diensten in de app."),
        ["fr"] = ("Horaire publié", "Votre horaire a été mis à jour. Consultez vos nouveaux services dans l'application."),
        ["en"] = ("Schedule published", "Your schedule has been updated. Check your new shifts in the app."),
    };

    private static readonly Dictionary<string, (string Title, string Body)> ChangedLabels = new()
    {
        ["nl"] = ("Rooster gewijzigd", "Je rooster is gewijzigd. Bekijk de details in de app."),
        ["fr"] = ("Horaire modifié", "Votre horaire a été modifié. Consultez les détails dans l'application."),
        ["en"] = ("Schedule changed", "Your schedule has changed. Check the details in the app."),
    };

    private static readonly Dictionary<string, (string Title, string Body)> CoveredLabels = new()
    {
        ["nl"] = ("Dienst overgenomen", "Je afwezige dienst is overgenomen door een collega."),
        ["fr"] = ("Service repris", "Votre service en votre absence a été repris par un(e) collègue."),
        ["en"] = ("Shift covered", "Your shift has been covered by a colleague."),
    };

    private static readonly Dictionary<string, (string Title, string Body)> CoverAssignedLabels = new()
    {
        ["nl"] = ("Nieuwe dienst toegewezen", "Je bent ingepland om een dienst over te nemen. Bekijk de details in de app."),
        ["fr"] = ("Nouveau service assigné", "Vous avez été assigné(e) pour reprendre un service. Consultez les détails dans l'application."),
        ["en"] = ("New shift assigned", "You've been assigned to cover a shift. Check the details in the app."),
    };

    public async Task NotifySchedulePublishedAsync(IReadOnlyCollection<Guid> staffProfileIds, CancellationToken cancellationToken = default)
    {
        if (staffProfileIds.Count == 0)
            return;

        var profiles = await db.StaffProfiles
            .Where(p => staffProfileIds.Contains(p.Id))
            .Select(p => new { p.Id, p.TenantUserId, p.PushToken })
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            db.Notifications.Add(new Notification
            {
                TenantUserId = profile.TenantUserId,
                Type = NotificationType.SchedulePublished,
                SourceId = profile.Id,
                TitleKey = "staff.notifications.schedule_published.title",
                BodyKey = "staff.notifications.schedule_published.body",
                ArgumentsJson = "{}",
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var profile in profiles)
            await SendPushAsync(profile.PushToken, PublishedLabels, cancellationToken);
    }

    // FR-007/FR-008: "your shift is changed" — a generic wording covers both a last-minute
    // non-cover edit and, via NotifyAbsentStaffCoveredAsync below, the more specific covered case.
    public async Task NotifyAssignmentChangedAsync(Guid staffProfileId, Guid sourceScheduleId, CancellationToken cancellationToken = default)
    {
        var profile = await db.StaffProfiles
            .Where(p => p.Id == staffProfileId)
            .Select(p => new { p.TenantUserId, p.PushToken })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
            return;

        db.Notifications.Add(new Notification
        {
            TenantUserId = profile.TenantUserId,
            Type = NotificationType.AssignmentChanged,
            SourceId = sourceScheduleId,
            TitleKey = "staff.notifications.assignment_changed.title",
            BodyKey = "staff.notifications.assignment_changed.body",
            ArgumentsJson = "{}",
        });
        await db.SaveChangesAsync(cancellationToken);
        await SendPushAsync(profile.PushToken, ChangedLabels, cancellationToken);
    }

    // FR-008a: the originally-absent staff member is told coverage is arranged — never the
    // replacement's name/identity, only that it's handled.
    public async Task NotifyAbsentStaffCoveredAsync(Guid staffProfileId, Guid sourceScheduleId, CancellationToken cancellationToken = default)
    {
        var profile = await db.StaffProfiles
            .Where(p => p.Id == staffProfileId)
            .Select(p => new { p.TenantUserId, p.PushToken })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
            return;

        db.Notifications.Add(new Notification
        {
            TenantUserId = profile.TenantUserId,
            Type = NotificationType.AssignmentChanged,
            SourceId = sourceScheduleId,
            TitleKey = "staff.notifications.assignment_covered.title",
            BodyKey = "staff.notifications.assignment_covered.body",
            ArgumentsJson = "{}",
        });
        await db.SaveChangesAsync(cancellationToken);
        await SendPushAsync(profile.PushToken, CoveredLabels, cancellationToken);
    }

    // FR-008a: the replacement is told about their own new assignment's details only (location,
    // group, date, time) — the client renders those from the in-app Notification's linked
    // StaffSchedule row (SourceId), never from the push body itself.
    public async Task NotifyCoverStaffAssignedAsync(Guid staffProfileId, Guid coverScheduleId, CancellationToken cancellationToken = default)
    {
        var profile = await db.StaffProfiles
            .Where(p => p.Id == staffProfileId)
            .Select(p => new { p.TenantUserId, p.PushToken })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
            return;

        db.Notifications.Add(new Notification
        {
            TenantUserId = profile.TenantUserId,
            Type = NotificationType.AssignmentChanged,
            SourceId = coverScheduleId,
            TitleKey = "staff.notifications.cover_assigned.title",
            BodyKey = "staff.notifications.cover_assigned.body",
            ArgumentsJson = "{}",
        });
        await db.SaveChangesAsync(cancellationToken);
        await SendPushAsync(profile.PushToken, CoverAssignedLabels, cancellationToken);
    }

    // contracts/staff-app-api.md's ReportSickCommand side effect — mirrors
    // EnrollmentNotificationService.NotifyDirectorsAsync's exact shape (Notification row only,
    // no push — directors use the web app, no push-token concept there).
    public async Task NotifyDirectorsOfSickReportAsync(StaffSchedule entry, string staffName, CancellationToken cancellationToken = default)
    {
        var directorIds = await db.Users
            .Where(u => u.Role == UserRole.Director)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (directorIds.Count == 0)
            return;

        var argumentsJson = JsonSerializer.Serialize(new { staffName, date = entry.Date.ToString("yyyy-MM-dd") });

        foreach (var directorId in directorIds)
        {
            db.Notifications.Add(new Notification
            {
                TenantUserId = directorId,
                Type = NotificationType.StaffSickReported,
                SourceId = entry.Id,
                TitleKey = "director.notifications.staff_sick_reported.title",
                BodyKey = "director.notifications.staff_sick_reported.body",
                ArgumentsJson = argumentsJson,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendPushAsync(string? pushToken, Dictionary<string, (string Title, string Body)> labels, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pushToken))
            return;

        var (title, body) = labels["nl"];
        try
        {
            await pushSender.SendAsync(pushToken, title, body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Staff schedule notification push dispatch failed.");
        }
    }
}
