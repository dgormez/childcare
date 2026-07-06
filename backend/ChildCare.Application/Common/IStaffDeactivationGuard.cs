namespace ChildCare.Application.Common;

/// <summary>
/// Extension point for features 009 (attendance/scheduling dependents) and 011 (caregiver
/// scheduling): each registers its own implementation via DI
/// (services.AddScoped&lt;IStaffDeactivationGuard, ...&gt;()) without overwriting the other's
/// registration — DeactivateStaffProfileCommandHandler resolves
/// IEnumerable&lt;IStaffDeactivationGuard&gt;, which is empty until either feature ships
/// (mirrors ILocationDeactivationGuard, feature 004). No implementation is registered by this
/// feature.
/// </summary>
public interface IStaffDeactivationGuard
{
    Task<bool> HasActiveDependentsAsync(Guid staffProfileId, ITenantDbContext db, CancellationToken cancellationToken = default);
}
