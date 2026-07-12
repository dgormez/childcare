using ChildCare.Application.Common;
using ChildCare.Application.DayReservations;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// FR-004/FR-005/FR-014/FR-018. Mode-change warning mirrors PublishClosureDayCommand's
/// ConfirmExistingAttendance pattern (feature 011) exactly: count what would be stranded,
/// require explicit confirmation before proceeding if anything is. Concurrency: a plain
/// full-object overwrite (FR-018) — deliberately no optimistic concurrency token, per spec.md's
/// documented last-write-wins decision for this low-frequency admin action (mirrors
/// UpdateLocationCommandHandler's own precedent).
/// </summary>
public class UpdateLocationReservationSettingsCommandHandler(
    ITenantDbContext db,
    ReservationPolicyResolver policyResolver) : IRequestHandler<UpdateLocationReservationSettingsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationReservationSettingsCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        ReservationModeMapper.TryParse(request.AbsencesMode, out var absencesMode);
        ReservationModeMapper.TryParse(request.ExtrasMode, out var extrasMode);
        ReservationModeMapper.TryParse(request.SwapsMode, out var swapsMode);

        if (!request.ConfirmDespitePending)
        {
            var pendingCounts = await CountPendingRequestsStrandedByChangeAsync(location, absencesMode, extrasMode, swapsMode, cancellationToken);
            if (pendingCounts.Count > 0)
                return LocationResult.Fail(LocationFailure.PendingRequestsWarning, pendingCounts);
        }

        location.ReservationAbsencesMode = absencesMode;
        location.ReservationExtrasMode = extrasMode;
        location.ReservationSwapsMode = swapsMode;
        location.ReservationNoticeHours = request.NoticeHours;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }

    /// <summary>
    /// FR-014: only warn for a type whose mode is changing *away from* Approval — a type that
    /// was already Disabled has no pending requests by construction, and Informational
    /// auto-approves instantly, so neither can have anything pending to strand.
    /// </summary>
    private async Task<Dictionary<string, int>> CountPendingRequestsStrandedByChangeAsync(
        Location location, ReservationRequestMode newAbsences, ReservationRequestMode newExtras, ReservationRequestMode newSwaps,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>();

        var changingTypes = new List<(DayReservationType Type, string Wire)>();
        if (location.ReservationAbsencesMode == ReservationRequestMode.Approval && newAbsences != ReservationRequestMode.Approval)
            changingTypes.Add((DayReservationType.Absence, "absence"));
        if (location.ReservationExtrasMode == ReservationRequestMode.Approval && newExtras != ReservationRequestMode.Approval)
            changingTypes.Add((DayReservationType.Extra, "extra"));
        if (location.ReservationSwapsMode == ReservationRequestMode.Approval && newSwaps != ReservationRequestMode.Approval)
            changingTypes.Add((DayReservationType.Exchange, "exchange"));

        if (changingTypes.Count == 0)
            return counts;

        var typesOnly = changingTypes.Select(t => t.Type).ToList();
        var pending = await db.DayReservations
            .Where(dr => dr.Status == DayReservationStatus.Pending && typesOnly.Contains(dr.Type))
            .Select(dr => new { dr.ChildId, dr.Type, dr.RequestedDate })
            .ToListAsync(cancellationToken);

        foreach (var (type, wire) in changingTypes)
        {
            var count = 0;
            foreach (var p in pending.Where(p => p.Type == type))
            {
                var candidateLocationIds = await policyResolver.ResolveCandidateLocationIdsAsync(p.ChildId, p.Type, p.RequestedDate, cancellationToken);
                if (candidateLocationIds.Contains(location.Id))
                    count++;
            }
            if (count > 0)
                counts[wire] = count;
        }

        return counts;
    }
}
