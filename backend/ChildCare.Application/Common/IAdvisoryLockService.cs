namespace ChildCare.Application.Common;

/// <summary>
/// Runs <paramref name="action"/> with exclusive access for <paramref name="key"/> — a
/// concurrent call for the same key blocks until this one completes (research.md R2), used to
/// serialize contract activation per child (FR-006) so two overlapping activation attempts for
/// the same child cannot both succeed when they would violate the one-active-per-location or
/// day-overlap rules. A new, feature-scoped port — not a promotion of
/// ITenantProvisioningService.RunExclusiveAsync (feature 001), which is deliberately left
/// untouched (research.md R2).
/// </summary>
public interface IAdvisoryLockService
{
    Task<T> RunExclusiveAsync<T>(Guid key, Func<Task<T>> action, CancellationToken cancellationToken = default);
}
