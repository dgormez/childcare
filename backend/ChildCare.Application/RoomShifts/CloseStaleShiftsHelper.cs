using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.RoomShifts;

/// <summary>
/// Lazy auto-checkout materialization (research.md R5) — no scheduled job, no Cloud Scheduler
/// wiring. Closes any RoomShift still open from a *previous* calendar day (local midnight
/// boundary) on the next read/write that touches the affected location, setting
/// ClosedReason = "auto_checkout" (spec FR-023). A childcare room tablet is realistically
/// touched constantly during operating hours, so this closes stale shifts essentially
/// immediately in practice.
/// </summary>
public class CloseStaleShiftsHelper(ITenantDbContext db)
{
    /// <summary>
    /// Closes every open shift at this location whose CheckedInAt falls before today's local
    /// midnight. <paramref name="localNow"/> is the caller-supplied current instant, intended to
    /// be in the tenant's configured timezone (spec Assumptions) — passed in rather than computed
    /// here so tests can control it deterministically. In practice, every current caller passes
    /// `DateTime.UtcNow` directly: no tenant/location timezone field exists anywhere in this
    /// codebase yet, so UTC stands in for "local" for now (a known simplification, not an
    /// oversight — revisit once a real timezone field is added to Tenant/Location).
    /// </summary>
    public async Task CloseStaleShiftsAsync(Guid locationId, DateTime localNow, CancellationToken cancellationToken = default)
    {
        var todayLocalMidnightUtc = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Utc);

        var staleShifts = await db.RoomShifts
            .Where(s => s.LocationId == locationId
                && s.CheckedOutAt == null
                && s.CheckedInAt < todayLocalMidnightUtc)
            .ToListAsync(cancellationToken);

        if (staleShifts.Count == 0)
            return;

        foreach (var shift in staleShifts)
        {
            shift.CheckedOutAt = todayLocalMidnightUtc;
            shift.ClosedReason = "auto_checkout";
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
