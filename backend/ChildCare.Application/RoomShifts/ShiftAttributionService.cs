using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.RoomShifts;

public class ShiftAttributionService(ITenantDbContext db) : IShiftAttributionService
{
    public async Task<IReadOnlyList<Guid>> ResolveRecordedByAsync(
        Guid locationId, Guid groupId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
        => await db.RoomShifts
            .Where(s => s.LocationId == locationId
                && s.GroupId == groupId
                && s.CheckedInAt <= occurredAtUtc
                && (s.CheckedOutAt == null || s.CheckedOutAt > occurredAtUtc))
            .Select(s => s.StaffProfileId)
            .ToListAsync(cancellationToken);
}
