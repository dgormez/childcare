using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.StaffLeaveRequests;

// FR-010/FR-008: notifies the requesting staff member when a director decides their leave
// request. Mirrors StaffScheduleNotificationService's shape (Notification row + best-effort
// push, "nl" default — StaffProfile has no locale field anywhere in this codebase).
public class StaffLeaveRequestNotificationService(ITenantDbContext db, IExpoPushSender pushSender, ILogger<StaffLeaveRequestNotificationService> logger)
{
    private static readonly (string Title, string ApprovedBody, string RejectedBody) Labels =
        ("Verlofaanvraag beslist", "Je verlofaanvraag is goedgekeurd.", "Je verlofaanvraag is afgewezen.");

    public async Task NotifyDecisionAsync(StaffLeaveRequest entry, CancellationToken cancellationToken = default)
    {
        var profile = await db.StaffProfiles
            .Where(p => p.Id == entry.StaffProfileId)
            .Select(p => new { p.TenantUserId, p.PushToken })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
            return;

        var approved = entry.Status == StaffLeaveRequestStatus.Approved;

        db.Notifications.Add(new Notification
        {
            TenantUserId = profile.TenantUserId,
            Type = NotificationType.LeaveRequestDecided,
            SourceId = entry.Id,
            TitleKey = "staff.notifications.leave_request_decided.title",
            BodyKey = approved
                ? "staff.notifications.leave_request_decided.approved_body"
                : "staff.notifications.leave_request_decided.rejected_body",
            ArgumentsJson = "{}",
        });
        await db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(profile.PushToken))
            return;

        try
        {
            await pushSender.SendAsync(
                profile.PushToken,
                Labels.Title,
                approved ? Labels.ApprovedBody : Labels.RejectedBody,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Leave request decision push dispatch failed for request {Id}.", entry.Id);
        }
    }
}
