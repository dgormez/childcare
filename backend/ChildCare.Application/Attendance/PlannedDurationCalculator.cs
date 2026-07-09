using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

/// <summary>
/// FR-006/FR-006a: derives `planned_duration_minutes` from the child's active contract at a
/// specific location for a specific weekday (research.md R6). A child may hold two simultaneous
/// contracts at two different locations (feature 007's split-location rule) — this always
/// matches against the contract for the given `locationId` only, never any of the child's other
/// contracts (FR-006).
/// </summary>
public class PlannedDurationCalculator(ITenantDbContext db)
{
    public async Task<int?> CalculateAsync(
        Guid childId, Guid locationId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var contract = await db.Contracts
            .Where(c => c.ChildId == childId && c.LocationId == locationId && c.Status == ContractStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);

        var contractedDay = contract?.ContractedDays.FirstOrDefault(d => d.Weekday == date.DayOfWeek);
        if (contractedDay is null)
            return null;

        return (int)(contractedDay.EndTime - contractedDay.StartTime).TotalMinutes;
    }
}
