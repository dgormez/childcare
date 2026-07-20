using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.GroupActivities;

/// <summary>
/// IGroupActivityChildDerivationService implementation (031-photo-lifecycle-governance R3) —
/// lives alongside GroupActivityMapper in the Application layer rather than Infrastructure,
/// since it needs nothing beyond ITenantDbContext (no GCS/external dependency). Applies the same
/// group-membership/date-overlap rule GetParentGroupActivityGalleryQuery already uses to decide
/// parent visibility, generalized from "this parent's children this month" to "every child in
/// this one activity's group on its date" — the two queries solve different shapes of the same
/// underlying question, so this is a semantically-equivalent reimplementation rather than a
/// literal code extraction (research.md R3).
/// </summary>
public class GroupActivityChildDerivationService(ITenantDbContext db) : IGroupActivityChildDerivationService
{
    public async Task<IReadOnlyList<Guid>> GetDepictedChildIdsAsync(Guid groupActivityId, CancellationToken cancellationToken = default)
    {
        var activity = await db.GroupActivities
            .FirstOrDefaultAsync(a => a.Id == groupActivityId, cancellationToken);
        if (activity is null)
            return [];

        var activityDate = DateOnly.FromDateTime(activity.OccurredAt);

        return await db.ChildGroupAssignments
            .Where(a => a.GroupId == activity.GroupId
                && a.StartDate <= activityDate
                && (a.EndDate == null || a.EndDate >= activityDate))
            .Select(a => a.ChildId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
