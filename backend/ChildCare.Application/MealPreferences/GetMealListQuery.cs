using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferences;

// contracts/meal-list-api.md, data-model.md. RestrictToGroupId scopes a device-token (caregiver
// tablet) caller to its own paired group (research.md R4) — null for a Director/Staff caller,
// who sees every group at the location. IncludeExpected powers the "Inclusief verwacht" toggle
// (research.md R2, added by feature 013d's US4 — see tasks.md T045).
public record GetMealListQuery(Guid LocationId, DateOnly Date, Guid? RestrictToGroupId, bool IncludeExpected = false)
    : IRequest<MealListResponse>;

public class GetMealListQueryHandler(ITenantDbContext db) : IRequestHandler<GetMealListQuery, MealListResponse>
{
    public async Task<MealListResponse> Handle(GetMealListQuery request, CancellationToken cancellationToken)
    {
        // FR-003/FR-004: only children currently physically present are included — Status must
        // be Present AND CheckOutAt must still be null. CheckOutCommand never changes Status
        // away from Present (only sets CheckOutAt), so Status alone would still show a child who
        // already left for the day — a real gap caught while writing this feature's own tests,
        // not assumed away.
        var presentRecords = await db.AttendanceRecords
            .Where(r => r.LocationId == request.LocationId && r.Date == request.Date
                        && r.Status == AttendanceStatus.Present && r.CheckOutAt == null)
            .ToListAsync(cancellationToken);
        var presentChildIds = presentRecords.Select(r => r.ChildId).ToList();

        var currentGroupByChild = await CurrentGroupByChildAsync(presentChildIds, request.Date, cancellationToken);

        if (request.RestrictToGroupId is Guid deviceGroupId)
        {
            presentChildIds = presentChildIds
                .Where(id => currentGroupByChild.TryGetValue(id, out var a) && a.GroupId == deviceGroupId)
                .ToList();
        }

        var childEntries = await BuildChildEntriesAsync(presentChildIds, request.Date, cancellationToken);

        var groupIds = currentGroupByChild.Values.Select(a => a.GroupId).Distinct().ToList();
        var groupNames = await db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var groups = childEntries
            .Where(e => currentGroupByChild.ContainsKey(e.ChildId))
            .GroupBy(e => currentGroupByChild[e.ChildId].GroupId)
            .Select(g => new MealListGroupEntry(g.Key, groupNames.GetValueOrDefault(g.Key, string.Empty), g.ToList()))
            .ToList();

        MealListExpectedEntry? expected = null;
        if (request.IncludeExpected)
        {
            var expectedChildIds = await ExpectedChildIdsAsync(request.LocationId, request.Date, cancellationToken);
            if (request.RestrictToGroupId is Guid restrictGroupId)
            {
                var expectedGroups = await CurrentGroupByChildAsync(expectedChildIds, request.Date, cancellationToken);
                expectedChildIds = expectedChildIds
                    .Where(id => expectedGroups.TryGetValue(id, out var a) && a.GroupId == restrictGroupId)
                    .ToList();
            }

            var expectedEntries = await BuildChildEntriesAsync(expectedChildIds, request.Date, cancellationToken);
            expected = new MealListExpectedEntry(expectedEntries);
        }

        return new MealListResponse(request.Date, groups, expected);
    }

    private async Task<Dictionary<Guid, ChildGroupAssignment>> CurrentGroupByChildAsync(
        List<Guid> childIds, DateOnly date, CancellationToken cancellationToken)
    {
        if (childIds.Count == 0) return [];

        var assignments = await db.ChildGroupAssignments
            .Where(a => childIds.Contains(a.ChildId) && a.StartDate <= date && (a.EndDate == null || a.EndDate > date))
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(a => a.ChildId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartDate).First());
    }

    private async Task<List<MealListChildEntry>> BuildChildEntriesAsync(
        List<Guid> childIds, DateOnly date, CancellationToken cancellationToken)
    {
        if (childIds.Count == 0) return [];

        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToListAsync(cancellationToken);

        var preferences = await db.MealPreferences
            .Where(p => childIds.Contains(p.ChildId))
            .ToDictionaryAsync(p => p.ChildId, cancellationToken);

        // Standing-medication validity window check (FR-008) happens client-side after a single
        // fetch — DeletedAt/RecordType are the only server-side filters, since ValidFrom/
        // ValidUntil's inclusive-boundary comparison against `date` is simplest evaluated in
        // memory for this small per-location child count (data-model.md, research.md R3).
        var standingMedicationRecords = await db.HealthRecords
            .Where(h => childIds.Contains(h.ChildId) && h.RecordType == HealthRecordType.MedicationStanding && h.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var medicatedChildIds = standingMedicationRecords
            .Where(h => (h.ValidFrom is null || h.ValidFrom <= date) && (h.ValidUntil is null || h.ValidUntil >= date))
            .Select(h => h.ChildId)
            .ToHashSet();

        return children
            .Select(c => MealListMapper.ToChildEntry(c, preferences.GetValueOrDefault(c.Id), medicatedChildIds.Contains(c.Id)))
            .ToList();
    }

    private async Task<List<Guid>> ExpectedChildIdsAsync(Guid locationId, DateOnly date, CancellationToken cancellationToken)
    {
        // Any attendance record at all (any status) for this date disqualifies a child from
        // "expected" — a child already marked Absent/Closure is not "expected", same as they're
        // excluded from the present view (research.md R2, tasks.md T044).
        var childIdsWithRecordToday = (await db.AttendanceRecords
            .Where(r => r.LocationId == locationId && r.Date == date)
            .Select(r => r.ChildId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var activeContracts = await db.Contracts
            .Where(c => c.LocationId == locationId && c.Status == ContractStatus.Active)
            .ToListAsync(cancellationToken);

        return activeContracts
            .Where(c => c.ContractedDays.Any(d => d.Weekday == date.DayOfWeek) && !childIdsWithRecordToday.Contains(c.ChildId))
            .Select(c => c.ChildId)
            .Distinct()
            .ToList();
    }
}
