using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Fulfils the extension point feature 006 reserved on IChildDeactivationGuard — a child with
/// an active contract cannot be deactivated (research.md R3).
/// </summary>
public class ContractChildDeactivationGuard : IChildDeactivationGuard
{
    public Task<bool> HasActiveDependentsAsync(Guid childId, ITenantDbContext db, CancellationToken cancellationToken = default)
        => db.Contracts.AnyAsync(c => c.ChildId == childId && c.Status == ContractStatus.Active, cancellationToken);
}
