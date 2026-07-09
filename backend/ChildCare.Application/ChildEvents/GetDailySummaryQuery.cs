using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ChildEvents;

public record GetDailySummaryQuery(Guid ChildId, DateOnly Date) : IRequest<DailySummaryResponse>;

public class GetDailySummaryQueryHandler(ITenantDbContext db) : IRequestHandler<GetDailySummaryQuery, DailySummaryResponse>
{
    public async Task<DailySummaryResponse> Handle(GetDailySummaryQuery request, CancellationToken cancellationToken)
    {
        // FR-018a: the requested date is a Europe/Brussels calendar day, the same boundary
        // ChildEventEditWindowPolicy uses (research.md R8/T007a) — resolved via the single
        // shared helper so the two can never silently drift apart (analyze finding C2).
        var (startUtc, endUtc) = BelgianCalendarDay.UtcRangeFor(request.Date);

        // FR-018: excludes staff-internal AND soft-deleted events uniformly — including from
        // every latest-value field below, not just the counts.
        var events = await db.ChildEvents
            .Where(e => e.ChildId == request.ChildId
                && e.DeletedAt == null
                && e.VisibleToParent
                && e.OccurredAt >= startUtc && e.OccurredAt < endUtc)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        var napsCount = events.Count(e => e.EventType == ChildEventType.Sleep && e.EndedAt.HasValue);
        var bottlesCount = events.Count(e => e.EventType == ChildEventType.FeedingBottle);
        var diaperChangesCount = events.Count(e => e.EventType == ChildEventType.Diaper);

        var latestMood = events.LastOrDefault(e => e.EventType == ChildEventType.Mood);
        var latestTemperature = events.LastOrDefault(e => e.EventType == ChildEventType.Temperature);
        // FR-017: reflects only that a qualifying event was recorded — independent of whether it
        // has a confirmed AdministeredBy attribution.
        var medicationAdministered = events.Any(e => e.EventType == ChildEventType.Medication);

        return new DailySummaryResponse(
            napsCount,
            bottlesCount,
            diaperChangesCount,
            latestMood is null ? null : ExtractString(latestMood.Payload, "value"),
            latestTemperature is null ? null : ExtractDecimal(latestTemperature.Payload, "celsius"),
            medicationAdministered);
    }

    private static string? ExtractString(string json, string field)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(field, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String
            ? el.GetString()
            : null;
    }

    private static decimal? ExtractDecimal(string json, string field)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(field, out var el) && el.TryGetDecimal(out var value) ? value : null;
    }
}
