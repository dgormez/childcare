using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests.VaccineTypes;

/// <summary>
/// Feature 013h (research.md R5, tasks.md T008): the public-schema equivalent of
/// TenantMigrationRolloutTests/LegacyVaccinationMigrationTests' revert-and-reapply pattern, for
/// the "AddVaccineTypeDeactivationAudit" migration on the genuinely-public (schema-less)
/// vaccine_types table. Unlike tenant-schema migrations, PublicDbContext has no per-tenant
/// "Ready tenants" rollout loop to exercise (research.md R3/R5 — there is only ever one public
/// schema) — this test instead proves the migration itself is reversible and re-appliable via
/// PublicDbContext.Database.MigrateAsync(), the same call OrganisationOnboardingWebAppFactory
/// already makes once at startup (InitializeAsync), and that 013g's pre-existing seeded catalog
/// rows survive both the revert and the reapply untouched.
/// </summary>
public class VaccineTypeDeactivationAuditMigrationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    /// <summary>
    /// Reverts just the "AddVaccineTypeDeactivationAudit" migration: drops its three new
    /// columns from the public "vaccine_types" table (no schema-name prefix needed — this table
    /// lives in the default/public schema, not a per-tenant one) and removes its
    /// __EFMigrationsHistory row. No later public-schema migration exists yet to worry about
    /// (unlike the tenant-schema revert helpers' "must also revert every later migration"
    /// concern) — this is currently the newest migration in Migrations/Public/.
    /// </summary>
    private static async Task RevertToPreDeactivationAuditAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "vaccine_types"
                DROP COLUMN "DeactivatedByUserId",
                DROP COLUMN "DeactivatedByEmail",
                DROP COLUMN "DeactivatedAt";
            DELETE FROM "__EFMigrationsHistory"
                WHERE "MigrationId" LIKE '%AddVaccineTypeDeactivationAudit';
            """);
    }

    private static async Task<bool> HasAuditColumnsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        try
        {
            _ = await publicDb.VaccineTypes.Select(v => v.DeactivatedByUserId).FirstOrDefaultAsync();
            _ = await publicDb.VaccineTypes.Select(v => v.DeactivatedByEmail).FirstOrDefaultAsync();
            _ = await publicDb.VaccineTypes.Select(v => v.DeactivatedAt).FirstOrDefaultAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Migrate_RevertedVaccineTypesTable_ReappliesAuditColumns_SeedRowsUntouched()
    {
        Assert.True(await HasAuditColumnsAsync(factory.Services));

        int seededCountBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            seededCountBefore = await publicDb.VaccineTypes.CountAsync();
        }

        await RevertToPreDeactivationAuditAsync(factory.Services);
        Assert.False(await HasAuditColumnsAsync(factory.Services));

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            await publicDb.Database.MigrateAsync();
        }

        Assert.True(await HasAuditColumnsAsync(factory.Services));

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            var seededCountAfter = await publicDb.VaccineTypes.CountAsync();
            Assert.Equal(seededCountBefore, seededCountAfter);

            // Existing rows (013g's seed) round-trip through the reapplied columns as null —
            // reapplying the migration must not touch pre-existing IsActive rows' state.
            var stillActive = await publicDb.VaccineTypes.Where(v => v.IsActive).ToListAsync();
            Assert.NotEmpty(stillActive);
            Assert.All(stillActive, v =>
            {
                Assert.Null(v.DeactivatedByUserId);
                Assert.Null(v.DeactivatedByEmail);
                Assert.Null(v.DeactivatedAt);
            });
        }
    }
}
