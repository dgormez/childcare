using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.GroupActivities;

// Current-month-only per spec.md Assumptions — Year/Month default to BelgianCalendarDay.Today()
// at the endpoint layer (mirrors GetDailySummaryQuery's date-default pattern).
public record GetParentGroupActivityGalleryQuery(Guid TenantUserId, int Year, int Month) : IRequest<GalleryResult>;

public class GalleryResult
{
    public bool Authorized { get; private init; }
    public GalleryResponse? Response { get; private init; }

    public static GalleryResult Ok(GalleryResponse response) => new() { Authorized = true, Response = response };
    public static GalleryResult Forbidden() => new() { Authorized = false };
}

public class GetParentGroupActivityGalleryQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    GroupActivityMapper mapper,
    IGroupActivityChildDerivationService derivationService) : IRequestHandler<GetParentGroupActivityGalleryQuery, GalleryResult>
{
    public async Task<GalleryResult> Handle(GetParentGroupActivityGalleryQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return GalleryResult.Forbidden();

        var (startUtc, endUtc) = BelgianCalendarDay.UtcRangeForMonth(request.Year, request.Month);
        var monthStart = new DateOnly(request.Year, request.Month, 1);
        var monthEndInclusive = monthStart.AddMonths(1).AddDays(-1);

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        if (childIds.Count == 0)
            return GalleryResult.Ok(new GalleryResponse([], HasConsent: false));

        // spec.md Edge Cases: an activity shared by two of this parent's children in the same
        // group (twins) must be de-duplicated, not shown once per child (research.md, T048).
        var groupIds = await db.ChildGroupAssignments
            .Where(a => childIds.Contains(a.ChildId)
                && a.StartDate <= monthEndInclusive
                && (a.EndDate == null || a.EndDate >= monthStart))
            .Select(a => a.GroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // hasConsent reflects whether ANY of the parent's children currently have photo consent
        // — distinguishes "no consent given" from "consent given, nothing recorded this month"
        // for the client's empty-state rendering (contracts/group-activities-api.md).
        var hasConsent = await db.Contracts
            .AnyAsync(c => childIds.Contains(c.ChildId) && c.Status == ContractStatus.Active && c.Consent.PhotosInternal, cancellationToken);

        if (groupIds.Count == 0 || !hasConsent)
            return GalleryResult.Ok(new GalleryResponse([], hasConsent));

        var activities = await db.GroupActivities
            .Where(a => groupIds.Contains(a.GroupId) && a.OccurredAt >= startUtc && a.OccurredAt < endUtc)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(cancellationToken);

        if (activities.Count == 0)
            return GalleryResult.Ok(new GalleryResponse([], hasConsent));

        var activeContracts = await db.Contracts
            .Where(c => childIds.Contains(c.ChildId) && c.Status == ContractStatus.Active)
            .ToListAsync(cancellationToken);

        var activityIds = activities.Select(a => a.Id).ToList();
        var photosByActivity = await db.GroupActivityPhotos
            .Where(p => activityIds.Contains(p.GroupActivityId))
            .GroupBy(p => p.GroupActivityId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        var items = new List<GalleryItemResponse>();
        foreach (var activity in activities)
        {
            if (!photosByActivity.TryGetValue(activity.Id, out var photos) || photos.Count == 0)
                continue; // Gallery only surfaces activities with at least one photo (spec.md Assumptions).

            // Consent gated per-location, same rule as the daily feed (research.md R6) — any of
            // this parent's active contracts at this activity's location with photos_internal.
            var consentedForThisActivity = activeContracts.Any(c => c.LocationId == activity.LocationId && c.Consent.PhotosInternal);
            if (!consentedForThisActivity)
                continue;

            // groupIds above is a month-level pre-filter; the derivation service is the exact,
            // per-activity-day authority on which children it depicts (031-photo-lifecycle-
            // governance R3) — the same gate GetParentPhotoDownloadUrlQuery applies, so a child
            // who left the group mid-month isn't shown photos taken after they left.
            var depictedChildIds = await derivationService.GetDepictedChildIdsAsync(activity.Id, cancellationToken);
            if (!depictedChildIds.Any(childIds.Contains))
                continue;

            foreach (var photo in photos)
            {
                var photoResponse = await mapper.ToPhotoResponseAsync(photo, cancellationToken);
                items.Add(new GalleryItemResponse(activity.Id, activity.GroupId, photoResponse, activity.OccurredAt));
            }
        }

        return GalleryResult.Ok(new GalleryResponse(items, hasConsent));
    }
}
