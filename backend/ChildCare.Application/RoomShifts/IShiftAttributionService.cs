namespace ChildCare.Application.RoomShifts;

/// <summary>
/// Reusable recorded_by/administered_by resolver for feature 009/010's command handlers
/// (data-model.md). A plain injectable domain service, not a MediatR command — it's a
/// read-time resolution step other features' handlers call directly, not an endpoint-triggered
/// write (plan.md Constitution Check).
/// </summary>
public interface IShiftAttributionService
{
    /// <summary>
    /// Every StaffProfileId with a RoomShift open (CheckedInAt &lt;= occurredAtUtc &amp;&amp;
    /// (CheckedOutAt == null || CheckedOutAt &gt; occurredAtUtc)) for that location/group at
    /// that instant (spec FR-015/016) — empty if nobody was checked in, one entry if exactly
    /// one caregiver was, multiple if more than one. Callers store the result as-is.
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveRecordedByAsync(
        Guid locationId, Guid groupId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
}
