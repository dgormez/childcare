using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChildCare.Api.Cli;

/// <summary>
/// The `backfill-growth-check` CLI subcommand body (feature 009a-child-events-custom-type,
/// contracts/child-events-api-delta.md, research.md R1/R2). One-time, per-tenant data value
/// rename (`measurement` -> `growth_check` on `child_events.event_type`) — mirrors
/// <see cref="MigrateTenantsCommand"/>'s tenant-loop structure, but runs a raw, schema-qualified
/// SQL data UPDATE via <see cref="PublicDbContext"/>'s connection (same pattern
/// ChildCare.Api.Tests' TenantMigrationRolloutTests already uses to reach into a specific
/// tenant schema) rather than an EF Core schema migration, since no column changes. MUST be run
/// against every tenant schema, as an explicit pre-deploy operator step, before deploying a
/// build whose <see cref="ChildEventTypeExtensions"/> no longer recognizes the literal
/// `"measurement"` wire value — otherwise EF Core's value converter throws reading any
/// un-migrated row.
/// </summary>
public static class BackfillGrowthCheckCommand
{
    /// <summary>Returns the process exit code: 0 if every tenant succeeded, 1 if any failed.</summary>
    public static async Task<int> RunAsync(IServiceProvider services)
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
                // Schema name comes from the trusted public Tenants table, never request
                // input — safe to interpolate into the identifier position (parameters can't
                // bind identifiers/table names, only values).
                var rowsUpdated = await publicDb.Database.ExecuteSqlRawAsync(
                    $"""UPDATE "{tenant.SchemaName}".child_events SET "EventType" = 'growth_check' WHERE "EventType" = 'measurement'""");

                Console.WriteLine($"{tenant.Slug}: {rowsUpdated} row(s) updated");
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
