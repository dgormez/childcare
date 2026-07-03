using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChildCare.Api.Cli;

/// <summary>
/// The `migrate-tenants` CLI subcommand body (contracts/migrate-tenants-cli.md, research.md R8).
/// Rolls a pending TenantDbContext migration out to every existing Ready tenant — an explicit,
/// operator-triggered action, never auto-applied (constitution Principle VI).
/// </summary>
public static class MigrateTenantsCommand
{
    /// <summary>Returns the process exit code: 0 if every tenant succeeded, 1 if any failed.</summary>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var publicDb = services.GetRequiredService<PublicDbContext>();
        var resolver = services.GetRequiredService<ITenantDbContextResolver>();

        var tenants = await publicDb.Tenants
            .Where(t => t.ProvisioningStatus == ProvisioningStatus.Ready)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var failureCount = 0;

        foreach (var tenant in tenants)
        {
            try
            {
                var db = resolver.ForSchema(tenant.SchemaName);
                var hadPendingMigrations = await db.HasPendingMigrationsAsync();
                await db.MigrateAsync();

                Console.WriteLine(hadPendingMigrations
                    ? $"{tenant.Slug}: migrated"
                    : $"{tenant.Slug}: already up to date");
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
