using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Fulfils the extension point feature 004 reserved on ILocationDeactivationGuard — a location
/// with an active contract cannot be deactivated (research.md R3).
/// </summary>
public class ContractLocationDeactivationGuard : ILocationDeactivationGuard
{
    public Task<bool> HasActiveDependentsAsync(Guid locationId, ITenantDbContext db, CancellationToken cancellationToken = default)
        => db.Contracts.AnyAsync(c => c.LocationId == locationId && c.Status == ContractStatus.Active, cancellationToken);
}
