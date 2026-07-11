using ChildCare.Application.ChildEvents;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.GroupActivities;

// research.md R4: one query, reused by both the caregiver-tablet (device-authenticated, today
// only) and director-web (DirectorOnly, explicit date) endpoints — mirrors GetDailySummaryQuery
// being reused by both the caregiver and parent endpoints.
public record GetGroupTimelineQuery(Guid GroupId, DateOnly Date) : IRequest<GroupTimelineResponse>;

public class GetGroupTimelineQueryHandler(ITenantDbContext db, GroupActivityMapper activityMapper)
    : IRequestHandler<GetGroupTimelineQuery, GroupTimelineResponse>
{
    public async Task<GroupTimelineResponse> Handle(GetGroupTimelineQuery request, CancellationToken cancellationToken)
    {
        var (startUtc, endUtc) = BelgianCalendarDay.UtcRangeFor(request.Date);

        var events = await db.ChildEvents
            .Where(e => e.GroupId == request.GroupId
                && e.DeletedAt == null
                && e.OccurredAt >= startUtc && e.OccurredAt < endUtc)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        var activities = await db.GroupActivities
            .Where(a => a.GroupId == request.GroupId && a.OccurredAt >= startUtc && a.OccurredAt < endUtc)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(cancellationToken);

        var activityIds = activities.Select(a => a.Id).ToList();
        var photosByActivity = await db.GroupActivityPhotos
            .Where(p => activityIds.Contains(p.GroupActivityId))
            .GroupBy(p => p.GroupActivityId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        var entries = new List<GroupTimelineEntryResponse>();

        foreach (var e in events)
            entries.Add(new GroupTimelineEntryResponse("child_event", e.OccurredAt, ChildEventMapper.ToResponse(e), null));

        foreach (var a in activities)
        {
            List<GroupActivityPhoto> photos = photosByActivity.TryGetValue(a.Id, out var list) ? list : [];
            var response = await activityMapper.ToResponseAsync(a, photos, cancellationToken);
            entries.Add(new GroupTimelineEntryResponse("group_activity", a.OccurredAt, null, response));
        }

        return new GroupTimelineResponse(entries.OrderBy(e => e.OccurredAt).ToList());
    }
}
