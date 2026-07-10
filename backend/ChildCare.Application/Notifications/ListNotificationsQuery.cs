using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Notifications;

public record ListNotificationsQuery(Guid TenantUserId) : IRequest<IReadOnlyList<NotificationResponse>>;

public class ListNotificationsQueryHandler(ITenantDbContext db) : IRequestHandler<ListNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<IReadOnlyList<NotificationResponse>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await db.Notifications
            .Where(n => n.TenantUserId == request.TenantUserId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        return notifications
            .Select(n => new NotificationResponse(n.Id, n.Type.ToString().ToLowerInvariant(), n.SourceId, n.TitleKey, n.BodyKey, n.ArgumentsJson, n.CreatedAt, n.ReadAt))
            .ToList();
    }
}
