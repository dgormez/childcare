namespace ChildCare.Application.Common;

/// <summary>
/// Extension point for features 005 (staff) and 007 (contracts): each registers its own
/// implementation via DI (services.AddScoped&lt;ILocationDeactivationGuard, ...&gt;()) without
/// overwriting the other's registration — DeactivateLocationCommandHandler resolves
/// IEnumerable&lt;ILocationDeactivationGuard&gt;, which is empty until either feature ships
/// (research.md R4). No implementation is registered by this feature.
/// </summary>
public interface ILocationDeactivationGuard
{
    Task<bool> HasActiveDependentsAsync(Guid locationId, ITenantDbContext db, CancellationToken cancellationToken = default);
}
