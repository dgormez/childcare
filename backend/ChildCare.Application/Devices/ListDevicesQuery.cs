using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Devices;

/// <summary>Feature 007a (spec.md FR-013a) — feature 008a built pairing/revocation but never a
/// list; this is the read the director web Devices screen needs. Returns every device, active
/// and revoked alike (the client distinguishes via RevokedAt), never a 404 for an empty
/// tenant.</summary>
public record ListDevicesQuery : IRequest<IReadOnlyList<DeviceSummaryResponse>>;

public class ListDevicesQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListDevicesQuery, IReadOnlyList<DeviceSummaryResponse>>
{
    public async Task<IReadOnlyList<DeviceSummaryResponse>> Handle(ListDevicesQuery request, CancellationToken cancellationToken)
    {
        var pairings = await db.DevicePairings.ToListAsync(cancellationToken);
        if (pairings.Count == 0)
            return [];

        var locationIds = pairings.Select(p => p.LocationId).Distinct().ToList();
        var groupIds = pairings.Select(p => p.GroupId).Distinct().ToList();
        var userIds = pairings.Select(p => p.PairedByTenantUserId).Distinct().ToList();

        var locationNames = await db.Locations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken);
        var groupNames = await db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
        var userNames = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return pairings
            .Select(p => new DeviceSummaryResponse(
                p.Id,
                p.LocationId,
                locationNames.GetValueOrDefault(p.LocationId, string.Empty),
                p.GroupId,
                groupNames.GetValueOrDefault(p.GroupId, string.Empty),
                p.PairedByTenantUserId,
                userNames.GetValueOrDefault(p.PairedByTenantUserId, string.Empty),
                p.CreatedAt,
                p.RevokedAt))
            .ToList();
    }
}
