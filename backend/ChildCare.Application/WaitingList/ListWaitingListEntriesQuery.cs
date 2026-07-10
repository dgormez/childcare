using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

/// <summary>
/// FR-003: defaults to Status = Waiting when request.Status is null ("waiting" not requested
/// explicitly). Pass "all" to see every status.
/// </summary>
public record ListWaitingListEntriesQuery(Guid LocationId, string? Status) : IRequest<ListWaitingListResult>;

public class ListWaitingListEntriesQueryHandler(ITenantDbContext db) : IRequestHandler<ListWaitingListEntriesQuery, ListWaitingListResult>
{
    public async Task<ListWaitingListResult> Handle(ListWaitingListEntriesQuery request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId, cancellationToken);
        if (!locationExists)
            return ListWaitingListResult.Fail(WaitingListFailure.LocationNotFound);

        var entries = await WaitingListQueries.BuildFilteredList(db, request.LocationId, request.Status, cancellationToken);
        return ListWaitingListResult.Success(entries);
    }
}

/// <summary>
/// Shared read-side logic between ListWaitingListEntriesQuery and
/// ReorderWaitingListEntryCommand (whose response is "the affected location's full re-sorted
/// list", contracts/waiting-list-api.md) — kept as one implementation so duplicate-detection
/// (FR-004) and status-filter semantics (FR-003) never drift between the two call sites.
/// </summary>
public static class WaitingListQueries
{
    public static async Task<IReadOnlyList<WaitingListEntryResponse>> BuildFilteredList(
        ITenantDbContext db, Guid locationId, string? status, CancellationToken cancellationToken)
    {
        // FR-004: duplicate detection compares against the full location roster regardless of
        // the status filter applied below, so a duplicate is never missed just because its twin
        // is hidden behind the default `waiting`-only view.
        var allForLocation = await db.WaitingListEntries
            .Where(e => e.LocationId == locationId)
            .ToListAsync(cancellationToken);

        var duplicateKeys = allForLocation
            .GroupBy(e => (e.ChildFirstName, e.ChildLastName, e.DateOfBirth))
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .ToHashSet();

        IEnumerable<WaitingListEntry> filtered;
        if (string.IsNullOrWhiteSpace(status) || status.Equals("waiting", StringComparison.OrdinalIgnoreCase))
        {
            filtered = allForLocation.Where(e => e.Status == WaitingListStatus.Waiting);
        }
        else if (status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = allForLocation;
        }
        else if (WaitingListMapper.TryParseStatus(status, out var parsedStatus))
        {
            filtered = allForLocation.Where(e => e.Status == parsedStatus);
        }
        else
        {
            filtered = allForLocation.Where(e => e.Status == WaitingListStatus.Waiting);
        }

        return filtered
            .OrderBy(e => e.Priority)
            .Select(e => WaitingListMapper.ToResponse(e, duplicateKeys.Contains((e.ChildFirstName, e.ChildLastName, e.DateOfBirth))))
            .ToList();
    }
}
