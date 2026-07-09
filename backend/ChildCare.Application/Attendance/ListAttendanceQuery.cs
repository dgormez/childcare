using System.Globalization;
using System.Text;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

// research.md R8: cursor pagination (not offset), same pattern as ListChildEventsQuery —
// director-web history/correction view, scoped to a single location/date.
public record ListAttendanceQuery(Guid LocationId, DateOnly Date, string? Before, int Limit) : IRequest<PagedAttendanceResponse>;

public class ListAttendanceQueryHandler(ITenantDbContext db) : IRequestHandler<ListAttendanceQuery, PagedAttendanceResponse>
{
    public async Task<PagedAttendanceResponse> Handle(ListAttendanceQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 100);

        var query = db.AttendanceRecords.Where(r => r.LocationId == request.LocationId && r.Date == request.Date);

        if (request.Before is not null && AttendanceCursor.TryDecode(request.Before, out var date, out var id))
            query = query.Where(r => r.Date < date || (r.Date == date && r.Id.CompareTo(id) < 0));

        var page = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        var items = page.Take(limit).ToList();
        var nextCursor = hasMore ? AttendanceCursor.Encode(items[^1].Date, items[^1].Id) : null;

        return new PagedAttendanceResponse(items.Select(AttendanceMapper.ToResponse).ToList(), nextCursor);
    }
}

internal static class AttendanceCursor
{
    public static string Encode(DateOnly date, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{date:O}|{id}"));

    public static bool TryDecode(string cursor, out DateOnly date, out Guid id)
    {
        date = default;
        id = default;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2) return false;
            // DateTimeStyles.RoundtripKind is meaningless for a date-only value (it governs
            // DateTimeKind/offset handling, which DateOnly has none of) and silently fails
            // TryParse when supplied — DateTimeStyles.None is the correct style here.
            if (!DateOnly.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return false;
            if (!Guid.TryParse(parts[1], out id)) return false;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
