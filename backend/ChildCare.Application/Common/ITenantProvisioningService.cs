namespace ChildCare.Application.Common;

/// <summary>
/// Port for provisioning a brand-new tenant schema (create schema, apply baseline
/// structure, seed the director row). Implemented in ChildCare.Infrastructure — see
/// research.md R6 for why this can't just be "call TenantDbContext.Database.Migrate()"
/// directly from a handler.
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>
    /// Returns the director user's actual persisted Id — under a genuine concurrent race this
    /// may differ from the `directorUserId` passed in (research.md R15). Callers MUST use the
    /// returned Id, not the one they passed in.
    /// </summary>
    Task<Guid> ProvisionAsync(
        string schemaName,
        Guid directorUserId,
        string directorEmail,
        string directorPasswordHash,
        string directorName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> with exclusive access for <paramref name="key"/> — a
    /// concurrent call for the same key blocks until this one completes (FR-015), so two
    /// overlapping registration attempts for the same invitation are serialized rather than
    /// both racing to provision. Without this, the loser of the Tenant-row race can observe
    /// the winner's row as "not Ready yet" and redo provisioning itself, also succeeding
    /// (research.md R15 follow-up).
    /// </summary>
    Task<T> RunExclusiveAsync<T>(Guid key, Func<Task<T>> action, CancellationToken cancellationToken = default);
}
