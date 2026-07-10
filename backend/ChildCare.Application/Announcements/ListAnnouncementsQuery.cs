using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Announcements;

public record ListAnnouncementsQuery : IRequest<IReadOnlyList<AnnouncementResponse>>;

public class ListAnnouncementsQueryHandler(ITenantDbContext db) : IRequestHandler<ListAnnouncementsQuery, IReadOnlyList<AnnouncementResponse>>
{
    public async Task<IReadOnlyList<AnnouncementResponse>> Handle(ListAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        var announcements = await db.Announcements
            .OrderByDescending(a => a.SentAt)
            .ToListAsync(cancellationToken);

        var counts = await db.AnnouncementRecipients
            .Where(r => announcements.Select(a => a.Id).Contains(r.AnnouncementId))
            .GroupBy(r => r.AnnouncementId)
            .Select(g => new { AnnouncementId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AnnouncementId, x => x.Count, cancellationToken);

        return announcements
            .Select(a => new AnnouncementResponse(
                a.Id, a.LocationId, a.GroupId, a.Subject, a.Body, a.SentByTenantUserId, a.SentAt,
                counts.TryGetValue(a.Id, out var count) ? count : 0))
            .ToList();
    }
}
