using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChildCare.Api.Cli;

/// <summary>
/// The `grant-platform-admin &lt;email&gt;` CLI subcommand body (spec.md Assumptions, research.md
/// R3). Grants TenantUser.IsPlatformAdmin = true to every director account matching the given
/// email, across every Ready tenant schema — the flag's only write path (FR-001; no in-app UI
/// grants or revokes it). Deliberately email-matched rather than tenant-scoped: if the same
/// email exists as a director account in more than one tenant, every matching account is granted
/// the flag (spec.md Assumptions — an accepted trade-off, not a gap, given how rare and
/// deliberate that collision would be). Mirrors MigrateTenantsCommand/BackfillGrowthCheckCommand's
/// per-tenant loop, try/catch, and per-tenant-result-plus-summary console output shape exactly,
/// so a run's effect is auditable from its own output alone — no separate persisted audit log.
/// </summary>
public static class GrantPlatformAdminCommand
{
    /// <summary>Returns the process exit code: 0 if every tenant succeeded, 1 if any failed.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string email)
    {
        var publicDb = services.GetRequiredService<PublicDbContext>();

        var tenants = await publicDb.Tenants
            .Where(t => t.ProvisioningStatus == ProvisioningStatus.Ready)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var failureCount = 0;

        foreach (var tenant in tenants)
        {
            try
            {
                // Schema name comes from the trusted public Tenants table, never request input —
                // safe to interpolate into the identifier position (parameters can't bind
                // identifiers/table names, only values, per BackfillGrowthCheckCommand's same
                // reasoning). The email is CLI-supplied, so it is passed as a genuine ADO
                // parameter ({0}), never interpolated into the SQL text itself.
                var rowsMatched = await publicDb.Database.ExecuteSqlRawAsync(
                    $"UPDATE \"{tenant.SchemaName}\".\"users\" SET \"IsPlatformAdmin\" = true WHERE \"Email\" = {{0}}",
                    email);

                Console.WriteLine(rowsMatched > 0
                    ? $"{tenant.Slug}: matched ({rowsMatched} account(s)) — granted"
                    : $"{tenant.Slug}: no match");
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.WriteLine($"{tenant.Slug}: failed — {ex.Message}");
            }
        }

        Console.WriteLine($"Summary: {tenants.Count - failureCount}/{tenants.Count} tenants succeeded.");

        return failureCount == 0 ? 0 : 1;
    }
}
