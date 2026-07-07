namespace ChildCare.Application.Common;

/// <summary>
/// Extension point for feature 007 (contracts): registers its own implementation via DI
/// (services.AddScoped&lt;IChildDeactivationGuard, ...&gt;()) once a child can have an active
/// contract — DeactivateChildCommandHandler resolves IEnumerable&lt;IChildDeactivationGuard&gt;,
/// which is empty until then (mirrors ILocationDeactivationGuard/IStaffDeactivationGuard,
/// features 004/005). No implementation is registered by this feature.
/// </summary>
public interface IChildDeactivationGuard
{
    Task<bool> HasActiveDependentsAsync(Guid childId, ITenantDbContext db, CancellationToken cancellationToken = default);
}
