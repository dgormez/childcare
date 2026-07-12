using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

/// <summary>Resolved effective policy for a single submission (never persisted — research.md R3).</summary>
public record ReservationPolicy(ReservationRequestMode Mode, int NoticeHours);

/// <summary>
/// Feature 013f. Resolves which location(s)' reservation settings govern a given
/// child/type/date, since <see cref="Domain.Entities.DayReservation"/> deliberately has no
/// <c>LocationId</c> (013a research.md R7 — a child can hold active contracts at multiple
/// locations simultaneously, feature 007's split-location rule). When more than one candidate
/// location applies, the most restrictive outcome governs (research.md R3): mirrors the
/// precedent <c>SubmitDayReservationCommandHandler</c>'s own exchange closure-day check already
/// set — reject if any candidate location has a closure, applied here as "reject if any
/// candidate disables the type."
/// </summary>
public class ReservationPolicyResolver(ITenantDbContext db)
{
    public async Task<ReservationPolicy> ResolveAsync(
        Guid childId, DayReservationType type, DateOnly requestedDate, CancellationToken cancellationToken = default)
    {
        var candidateLocationIds = await ResolveCandidateLocationIdsAsync(childId, type, requestedDate, cancellationToken);
        if (candidateLocationIds.Count == 0)
            return new ReservationPolicy(ReservationRequestMode.Approval, 0);

        var candidates = await db.Locations
            .Where(l => candidateLocationIds.Contains(l.Id))
            .Select(l => new
            {
                l.ReservationAbsencesMode,
                l.ReservationExtrasMode,
                l.ReservationSwapsMode,
                l.ReservationNoticeHours,
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return new ReservationPolicy(ReservationRequestMode.Approval, 0);

        var modes = type switch
        {
            DayReservationType.Absence => candidates.Select(c => c.ReservationAbsencesMode).ToList(),
            DayReservationType.Extra => candidates.Select(c => c.ReservationExtrasMode).ToList(),
            DayReservationType.Exchange => candidates.Select(c => c.ReservationSwapsMode).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unhandled DayReservationType."),
        };

        ReservationRequestMode effectiveMode;
        if (modes.Any(m => m == ReservationRequestMode.Disabled))
            effectiveMode = ReservationRequestMode.Disabled;
        else if (modes.All(m => m == ReservationRequestMode.Informational))
            effectiveMode = ReservationRequestMode.Informational;
        else
            effectiveMode = ReservationRequestMode.Approval;

        var noticeHours = candidates.Max(c => c.ReservationNoticeHours);
        return new ReservationPolicy(effectiveMode, noticeHours);
    }

    /// <summary>
    /// Also used by <c>UpdateLocationReservationSettingsCommandHandler</c> (FR-014) to check
    /// whether a specific location is a candidate for an existing pending reservation, so a
    /// director can be warned before a mode change strands it.
    /// </summary>
    public async Task<HashSet<Guid>> ResolveCandidateLocationIdsAsync(
        Guid childId, DayReservationType type, DateOnly requestedDate, CancellationToken cancellationToken = default)
    {
        if (type == DayReservationType.Absence)
        {
            var weekdayMatches = await db.Contracts
                .Where(c => c.ChildId == childId && c.Status == ContractStatus.Active)
                .SelectMany(c => c.ContractedDays, (c, d) => new { c.LocationId, d.Weekday })
                .Where(x => x.Weekday == requestedDate.DayOfWeek)
                .Select(x => x.LocationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (weekdayMatches.Count > 0)
                return weekdayMatches.ToHashSet();
            // No contracted weekday match for this date — fall back to every active-contract
            // location (research.md R3), same as the "extra"/"exchange" branch below.
        }

        var allActiveContractLocations = await db.Contracts
            .Where(c => c.ChildId == childId && c.Status == ContractStatus.Active)
            .Select(c => c.LocationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return allActiveContractLocations.ToHashSet();
    }
}
