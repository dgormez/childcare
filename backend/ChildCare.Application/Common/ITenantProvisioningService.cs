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
}
