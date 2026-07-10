using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Notifications;

/// <summary>Returns false if the notification doesn't exist or doesn't belong to the caller — the
/// endpoint maps either to the same 403/404 (FR-011, no cross-parent access).</summary>
public record MarkNotificationReadCommand(Guid TenantUserId, Guid NotificationId) : IRequest<bool>;

public class MarkNotificationReadCommandHandler(ITenantDbContext db) : IRequestHandler<MarkNotificationReadCommand, bool>
{
    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.TenantUserId == request.TenantUserId, cancellationToken);
        if (notification is null)
            return false;

        // FR-011: marking one notification read must never affect another's read state — a
        // single-row update by primary key, nothing else touched.
        notification.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
