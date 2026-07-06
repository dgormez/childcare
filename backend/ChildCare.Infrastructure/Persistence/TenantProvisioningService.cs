using System.Text.RegularExpressions;
using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Provisions a brand-new tenant schema: creates it, applies the baseline TenantDbContext
/// migrations to it, and seeds the director's user row.
///
/// EF Core migrations bake their schema name in as a literal string at scaffold time —
/// changing TenantDbContext's runtime HasDefaultSchema() does NOT retroactively change an
/// already-generated migration's hardcoded schema. So this service generates the baseline
/// migration SQL fresh (via IMigrator, against the "tenant_template" placeholder schema
/// TenantDbContextFactory scaffolds against), substitutes the real tenant schema name into
/// that SQL text, and executes the result directly — the standard technique for EF Core
/// schema-per-tenant with a shared migration source (research.md R6).
/// </summary>
public class TenantProvisioningService(IConfiguration configuration) : ITenantProvisioningService
{
    private const string PlaceholderSchema = "tenant_template";
    private static readonly Regex SafeSchemaName = new("^[a-z][a-z0-9_]{0,62}$", RegexOptions.Compiled);

    // GenerateBaselineSql()'s output depends only on the compiled migrations, never on the
    // real tenant schema name (that's substituted afterwards) — deterministic across calls,
    // so it's computed once and reused for the lifetime of this (Singleton) service instead
    // of re-running EF's migrator on every registration request.
    private static readonly Lazy<string> BaselineSqlTemplate = new(GenerateBaselineSql);

    /// <summary>
    /// Test-only seam (tasks.md T049): if set, invoked after the schema exists but before the
    /// baseline tables are created, so integration tests can deterministically simulate a
    /// mid-provisioning failure (spec.md US3). Never set outside tests.
    /// </summary>
    public Action? FailureInjectionHookForTests { get; set; }

    /// <summary>
    /// Returns the director user's actual persisted Id — which, under a genuine concurrent
    /// race, may not be the `directorUserId` this particular call passed in (research.md R15):
    /// the upsert is by email, so if another concurrent call's insert already won, this call's
    /// own candidate Id is discarded and the real one is returned instead. Callers MUST use
    /// the returned Id (e.g., for the access token), not the Id they passed in.
    /// </summary>
    public async Task<Guid> ProvisionAsync(
        string schemaName,
        Guid directorUserId,
        string directorEmail,
        string directorPasswordHash,
        string directorName,
        CancellationToken ct = default)
    {
        if (!SafeSchemaName.IsMatch(schemaName))
            throw new ArgumentException($"'{schemaName}' is not a valid Postgres schema identifier.", nameof(schemaName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Idempotent — safe to re-run on a retried registration (FR-014).
        await using (var createSchemaCmd = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"", connection))
            await createSchemaCmd.ExecuteNonQueryAsync(ct);

        FailureInjectionHookForTests?.Invoke();

        // Idempotent (T050/T051): a resumed attempt may find the baseline already applied from
        // a prior failed run. The generated script (MigrationsSqlGenerationOptions.Default) is
        // itself wrapped in an explicit START TRANSACTION/COMMIT, so a single execution attempt
        // is all-or-nothing — a mid-batch failure can never leave the baseline half-applied.
        // That means "DuplicateTable" on a retry can only mean an EARLIER attempt's transaction
        // committed in full, so everything it created (table, index, history row) already
        // exists — safe to treat as "already provisioned, nothing to do" rather than a real
        // failure, never a sign of a partially-applied batch.
        var baselineSql = BaselineSqlTemplate.Value.Replace(PlaceholderSchema, schemaName);
        if (baselineSql.Contains(PlaceholderSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Baseline migration SQL still references the placeholder schema '{PlaceholderSchema}' after " +
                "substitution — refusing to run it, since that would write into the shared placeholder schema " +
                "instead of the tenant's own schema.");

        try
        {
            await using var baselineCmd = new NpgsqlCommand(baselineSql, connection);
            await baselineCmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.DuplicateTable or PostgresErrorCodes.DuplicateObject)
        {
            // Already applied by an earlier, fully-successful attempt — safe to continue. But
            // the baseline script's own embedded transaction is now aborted at the Postgres
            // session level; every subsequent command on this connection (the upsert below)
            // would fail with 25P02 "current transaction is aborted" without an explicit
            // ROLLBACK first — Postgres doesn't clear that state on its own.
            await using var rollbackCmd = new NpgsqlCommand("ROLLBACK", connection);
            await rollbackCmd.ExecuteNonQueryAsync(ct);
        }

        // Upsert-by-email, returning the row's ACTUAL Id — idempotent so a resumed attempt
        // never fails on a duplicate director row, and correct under a genuine concurrent
        // race: "DO UPDATE SET ... a no-op" (rather than DO NOTHING) is required for RETURNING
        // to still produce a row when a concurrent insert already won (research.md R15).
        // "Role" is set explicitly here rather than relying on the AddUserRole migration's
        // backfill-only column default (research.md R3) — every new tenant's director row
        // states its role outright.
        var upsertDirectorSql =
            $"""
             SET search_path TO "{schemaName}";
             INSERT INTO users ("Id", "Email", "PasswordHash", "Name", "Role", "CreatedAt")
             VALUES (@id, @email, @passwordHash, @name, 'director', now())
             ON CONFLICT ("Email") DO UPDATE SET "Email" = EXCLUDED."Email"
             RETURNING "Id"
             """;
        await using var upsertCmd = new NpgsqlCommand(upsertDirectorSql, connection);
        upsertCmd.Parameters.AddWithValue("id", directorUserId);
        upsertCmd.Parameters.AddWithValue("email", directorEmail);
        upsertCmd.Parameters.AddWithValue("passwordHash", directorPasswordHash);
        upsertCmd.Parameters.AddWithValue("name", directorName);

        var actualDirectorId = (Guid)(await upsertCmd.ExecuteScalarAsync(ct))!;
        return actualDirectorId;
    }

    /// <summary>
    /// Serializes concurrent calls for the same <paramref name="key"/> via a Postgres session-
    /// level advisory lock, held on a dedicated connection for the duration of <paramref
    /// name="action"/> only. Advisory locks are server-side and keyed by an int64, not tied to
    /// any particular connection pool — a lock taken here blocks any other caller (this
    /// service's own registration handling, in practice) requesting the same key on any
    /// connection, and is automatically released if this connection drops, so a crash mid-
    /// registration can never leave the lock stuck.
    /// </summary>
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

    private static string GenerateBaselineSql()
    {
        // SQL generation never opens a connection, so this connection string only needs to be
        // syntactically valid for the Npgsql provider to configure itself — it's never dialed.
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=script-generation-only",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PlaceholderSchema));
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();

        using var context = new TenantDbContext(optionsBuilder.Options, PlaceholderSchema);
        var migrator = context.GetService<IMigrator>();
        return migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Default);
    }
}
