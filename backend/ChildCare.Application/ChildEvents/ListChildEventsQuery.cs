using System.Globalization;
using System.Text;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ChildEvents;

// research.md R6: cursor pagination (not offset) on the caregiver-tablet-facing timeline —
// no visibleToParent filtering here, this is the full caregiver timeline (contracts/
// child-events-api.md); a future parent-facing endpoint adds its own filter.
public record ListChildEventsQuery(Guid ChildId, string? Before, int Limit) : IRequest<PagedChildEventsResponse>;

public class ListChildEventsQueryHandler(ITenantDbContext db) : IRequestHandler<ListChildEventsQuery, PagedChildEventsResponse>
{
    public async Task<PagedChildEventsResponse> Handle(ListChildEventsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 100);

        var query = db.ChildEvents.Where(e => e.ChildId == request.ChildId && e.DeletedAt == null);

        if (request.Before is not null && ChildEventCursor.TryDecode(request.Before, out var occurredAt, out var id))
        {
            query = query.Where(e =>
                e.OccurredAt < occurredAt || (e.OccurredAt == occurredAt && e.Id.CompareTo(id) < 0));
        }

        var page = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        var items = page.Take(limit).ToList();
        var nextCursor = hasMore ? ChildEventCursor.Encode(items[^1].OccurredAt, items[^1].Id) : null;

        return new PagedChildEventsResponse(items.Select(ChildEventMapper.ToResponse).ToList(), nextCursor);
    }
}

internal static class ChildEventCursor
{
    public static string Encode(DateTime occurredAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{occurredAt:O}|{id}"));

    public static bool TryDecode(string cursor, out DateTime occurredAt, out Guid id)
    {
        occurredAt = default;
        id = default;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2) return false;
            if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out occurredAt)) return false;
            if (!Guid.TryParse(parts[1], out id)) return false;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
