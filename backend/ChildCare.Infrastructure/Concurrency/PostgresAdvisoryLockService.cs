using ChildCare.Application.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ChildCare.Infrastructure.Concurrency;

/// <summary>
/// Implements IAdvisoryLockService using pg_advisory_lock/pg_advisory_unlock on a dedicated
/// connection — the same technique as TenantProvisioningService.RunExclusiveAsync (feature
/// 001, commit 4625480), reimplemented here rather than shared, since that method lives on a
/// feature-001-specific interface (research.md R2). Advisory locks are database-wide (not
/// schema-scoped), so this works correctly regardless of which tenant schema's search_path is
/// active — all tenant schemas live in the same physical Postgres database.
/// </summary>
public class PostgresAdvisoryLockService(IConfiguration configuration) : IAdvisoryLockService
{
    public async Task<T> RunExclusiveAsync<T>(Guid key, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var lockKey = BitConverter.ToInt64(key.ToByteArray(), 0);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection))
        {
            lockCmd.Parameters.AddWithValue("key", lockKey);
            await lockCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            return await action();
        }
        finally
        {
            // Always attempt the unlock, even if `action` was cancelled — an un-cancellable
            // token so a shutting-down request doesn't skip releasing the lock it holds.
            await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
            unlockCmd.Parameters.AddWithValue("key", lockKey);
            await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
