namespace ChildCare.Application.Common;

/// <summary>
/// Derives which children a group-activity photo depicts, for governance purposes only
/// (031-photo-lifecycle-governance) — 009b never built a per-photo child-tagging table, so this
/// reuses the same signal <c>GetParentGroupActivityGalleryQuery</c> relies on to decide whether a
/// parent may see an activity's photos: every child whose <c>ChildGroupAssignment</c> places them
/// in the activity's group on the activity's date. Used by the photo-archival job (eligibility)
/// and the GDPR purge command (sole-depicted-child check) so both apply an identical rule.
/// </summary>
public interface IGroupActivityChildDerivationService
{
    /// <summary>Returns an empty list if the activity does not exist.</summary>
    Task<IReadOnlyList<Guid>> GetDepictedChildIdsAsync(Guid groupActivityId, CancellationToken cancellationToken = default);
}
