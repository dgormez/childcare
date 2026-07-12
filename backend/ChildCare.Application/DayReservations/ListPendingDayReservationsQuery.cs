using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

/// <summary>The director's approval queue (FR-006). Defaults to Pending, newest-CreatedAt-first.</summary>
public record ListPendingDayReservationsQuery(string? Status) : IRequest<ListDayReservationsResult>;

public class ListPendingDayReservationsQueryHandler(ITenantDbContext db) : IRequestHandler<ListPendingDayReservationsQuery, ListDayReservationsResult>
{
    public async Task<ListDayReservationsResult> Handle(ListPendingDayReservationsQuery request, CancellationToken cancellationToken)
    {
        // Mirrors ListWaitingListEntriesQuery's status-filter precedent: "all" is a distinct
        // case from an unparseable/absent value, which still defaults to Pending (the queue's
        // natural default view), rather than "all" silently falling through to Pending too.
        IQueryable<Domain.Entities.DayReservation> query = db.DayReservations.AsNoTracking();
        if (string.IsNullOrWhiteSpace(request.Status) || request.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == DayReservationStatus.Pending);
        }
        else if (!request.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var status = DayReservationMapper.TryParseStatus(request.Status, out var parsed) ? parsed : DayReservationStatus.Pending;
            query = query.Where(x => x.Status == status);
        }

        var reservations = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var childIds = reservations.Select(r => r.ChildId).Distinct().ToList();
        var children = await db.Children
            .Where(c => childIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}", cancellationToken);

        var responses = new List<DayReservationResponse>(reservations.Count);
        foreach (var reservation in reservations)
        {
            var childName = children.TryGetValue(reservation.ChildId, out var name) ? name : "";
            bool? capacityWarning = reservation.Type == DayReservationType.Extra
                ? await ComputeCapacityWarningAsync(reservation.ChildId, reservation.RequestedDate, cancellationToken)
                : null;
            responses.Add(DayReservationMapper.ToResponse(reservation, childName, capacityWarning));
        }

        return ListDayReservationsResult.Success(responses);
    }

    /// <summary>
    /// research.md R5: reuses feature 012a's active-contracts-vs-Location.MaxCapacity occupancy
    /// computation, never attendance (which doesn't exist for future dates). Checked against
    /// every location the child holds an active contract at (research.md R7) — an extra day has
    /// no location of its own to check against, unlike a Absence/Exchange whose target weekday
    /// resolves to exactly one.
    /// </summary>
    private async Task<bool?> ComputeCapacityWarningAsync(Guid childId, DateOnly date, CancellationToken cancellationToken)
    {
        var locationIds = await db.Contracts
            .Where(c => c.ChildId == childId && c.Status == ContractStatus.Active)
            .Select(c => c.LocationId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (locationIds.Count == 0)
            return null;

        foreach (var locationId in locationIds)
        {
            var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId && l.DeactivatedAt == null, cancellationToken);
            if (location is null)
                continue;

            var occupiedCount = await db.Contracts
                .Where(c => c.LocationId == locationId && c.Status == ContractStatus.Active
                    && c.StartDate <= date && (c.EndDate == null || c.EndDate >= date))
                .SelectMany(c => c.ContractedDays)
                .CountAsync(d => d.Weekday == date.DayOfWeek, cancellationToken);

            if (occupiedCount >= location.MaxCapacity)
                return true;
        }

        return false;
    }
}
