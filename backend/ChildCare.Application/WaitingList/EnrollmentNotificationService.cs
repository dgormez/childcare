using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

/// <summary>
/// Notifies every director in the tenant when a new self-registered waiting-list entry arrives
/// (spec.md FR-010, data-model.md). The first feature to target a director as an in-app
/// `Notification` recipient directly — every prior use (DayReservationNotificationService,
/// TemperatureAlertService, etc.) resolves a parent/contact's `TenantUserId` first. Directors
/// are tenant-wide, not location-scoped (`TenantUser` has no location-assignment field, unlike
/// `StaffLocationEligibility`), so every director in the schema is notified regardless of which
/// location the entry is for — director-web's own location filter narrows the waiting-list view
/// from there. No push channel: directors use the web app, which has no push-token concept
/// (unlike the parent/caregiver apps' Expo push registration).
///
/// Takes `db` as a parameter, not a constructor-injected ambient `ITenantDbContext` — this
/// service is called from SubmitPublicEnrollmentCommandHandler on a tenant-exempt public route
/// with no JWT `tenant_id` claim, so the ambient `ITenantDbContext` (which resolves its schema
/// from `ICurrentTenantService`) would throw; the caller's already-resolved,
/// `tenantResolver.ForSchema(...)`-obtained context must be threaded through explicitly instead.
/// </summary>
public class EnrollmentNotificationService
{
    public async Task NotifyDirectorsAsync(ITenantDbContext db, WaitingListEntry entry, CancellationToken cancellationToken = default)
    {
        var directorIds = await db.Users
            .Where(u => u.Role == UserRole.Director)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (directorIds.Count == 0)
            return;

        var argumentsJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            childName = $"{entry.ChildFirstName} {entry.ChildLastName}",
        });

        foreach (var directorId in directorIds)
        {
            db.Notifications.Add(new Notification
            {
                TenantUserId = directorId,
                Type = NotificationType.EnrollmentSubmitted,
                SourceId = entry.Id,
                TitleKey = "director.notifications.enrollment_submitted.title",
                BodyKey = "director.notifications.enrollment_submitted.body",
                ArgumentsJson = argumentsJson,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
