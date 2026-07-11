using ChildCare.Application.Common;
using ChildCare.Application.GroupActivities;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ChildEvents;

public record GetDailySummaryQuery(Guid ChildId, DateOnly Date) : IRequest<DailySummaryResponse>;

public class GetDailySummaryQueryHandler(ITenantDbContext db, GroupActivityMapper groupActivityMapper) : IRequestHandler<GetDailySummaryQuery, DailySummaryResponse>
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

        // Feature 013 (specs/013-parent-communication/research.md R5): oldest-first activity
        // descriptions. Photos are explicitly out of scope — see that feature's spec.md.
        var activities = events
            .Where(e => e.EventType == ChildEventType.Activity)
            .Select(e => ExtractString(e.Payload, "description"))
            .Where(description => description is not null)
            .Select(description => description!)
            .ToList();

        var groupActivities = await ResolveGroupActivitiesAsync(request.ChildId, request.Date, startUtc, endUtc, cancellationToken);

        return new DailySummaryResponse(
            napsCount,
            bottlesCount,
            diaperChangesCount,
            latestMood is null ? null : ExtractString(latestMood.Payload, "value"),
            latestTemperature is null ? null : ExtractDecimal(latestTemperature.Payload, "celsius"),
            medicationAdministered,
            activities,
            groupActivities);
    }

    // research.md R5/R6: resolves the child's group(s) as of the requested date (a child may
    // have more than one active ChildGroupAssignment — feature 007's split-location contracts
    // mean a child can be enrolled at two locations, each with its own group, on the same day),
    // fetches that group's activities for the date, and gates each activity's photos on the
    // active contract *at that activity's own location* — consent is set per contract/location
    // (feature 007), not globally per child.
    private async Task<IReadOnlyList<GroupActivitySummaryItem>> ResolveGroupActivitiesAsync(
        Guid childId, DateOnly date, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var groupIds = await db.ChildGroupAssignments
            .Where(a => a.ChildId == childId && a.StartDate <= date && (a.EndDate == null || a.EndDate >= date))
            .Select(a => a.GroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (groupIds.Count == 0)
            return [];

        var groupActivities = await db.GroupActivities
            .Where(a => groupIds.Contains(a.GroupId) && a.OccurredAt >= startUtc && a.OccurredAt < endUtc)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(cancellationToken);

        if (groupActivities.Count == 0)
            return [];

        var activeContracts = await db.Contracts
            .Where(c => c.ChildId == childId
                && c.Status == ContractStatus.Active
                && c.StartDate <= date && (c.EndDate == null || c.EndDate >= date))
            .ToListAsync(cancellationToken);

        var groupActivityIds = groupActivities.Select(a => a.Id).ToList();
        var photosByActivity = await db.GroupActivityPhotos
            .Where(p => groupActivityIds.Contains(p.GroupActivityId))
            .GroupBy(p => p.GroupActivityId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        var result = new List<GroupActivitySummaryItem>(groupActivities.Count);
        foreach (var activity in groupActivities)
        {
            // FR-009: photos only when the child has an active contract, at this activity's own
            // location, with photos_internal = true — absent or false, title/description still
            // return but photos is [].
            var hasConsent = activeContracts.Any(c => c.LocationId == activity.LocationId && c.Consent.PhotosInternal);
            IReadOnlyList<GroupActivityPhotoResponse> photos = hasConsent && photosByActivity.TryGetValue(activity.Id, out var list)
                ? await Task.WhenAll(list.Select(p => groupActivityMapper.ToPhotoResponseAsync(p, cancellationToken)))
                : [];

            result.Add(new GroupActivitySummaryItem(
                activity.Id,
                activity.ActivityType.ToWireString(),
                activity.Title,
                activity.Description,
                activity.OccurredAt,
                photos));
        }

        return result;
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
