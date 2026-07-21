using ChildCare.Application.Common;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Locations;

/// <summary>
/// Feature 023, tasks.md T066 (research.md R1/data-model.md): the "AddDigitalEnrollment"
/// migration's slug-backfill SQL must assign a unique, non-null PublicEnrollmentSlug to every
/// pre-existing Location row before the column is locked NOT NULL + unique below it — simulates
/// a tenant that provisioned before this feature shipped, with locations whose names collide
/// once slugified (exact duplicates, a punctuation-only variant of the same name, and a name
/// with no alphanumeric characters at all). Same revert-then-reapply pattern as
/// LegacyVaccinationMigrationTests. AddDigitalEnrollment is currently the newest tenant
/// migration, so only it needs reverting here — no chain of later migrations to also undo
/// (unlike TenantMigrationRolloutTests/LegacyVaccinationMigrationTests, which revert the oldest
/// of several).
/// </summary>
public class PublicEnrollmentSlugBackfillMigrationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task RevertToPreDigitalEnrollmentAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync($"""
            DROP INDEX "{schemaName}"."IX_waiting_list_entries_LocationId_ChildFirstName_ChildLastNam~";
            DROP INDEX "{schemaName}"."IX_waiting_list_entries_ReferenceCode";
            DROP INDEX "{schemaName}"."IX_locations_PublicEnrollmentSlug";
            ALTER TABLE "{schemaName}"."waiting_list_entries"
                DROP COLUMN "ReferenceCode",
                DROP COLUMN "Source",
                DROP COLUMN "SubmittedLocale",
                DROP COLUMN "TourInvitationSentAt",
                DROP COLUMN "TourInvitationStatus",
                DROP COLUMN "TourOutcome",
                DROP COLUMN "TourProposedAt";
            ALTER TABLE "{schemaName}"."locations"
                DROP COLUMN "DefaultEnrollmentLocale",
                DROP COLUMN "PublicEnrollmentEnabled",
                DROP COLUMN "PublicEnrollmentSlug";
            DELETE FROM "{schemaName}"."__EFMigrationsHistory"
                WHERE "MigrationId" LIKE '%AddDigitalEnrollment';
            """);
    }

    [Fact]
    public async Task Migrate_BackfillsSlugsForPreExistingLocations_AllUniqueNoCollisions()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Slug Backfill Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        await RevertToPreDigitalEnrollmentAsync(factory.Services, schemaName);

        // Four locations, inserted directly (the pre-revert schema has no slug column yet), whose
        // names collide once slugified: two exact duplicates, one that differs only in
        // punctuation (same base slug as the duplicates), and one with no alphanumeric
        // characters at all (must fall back to the "location" base rather than an empty slug).
        var locationIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var names = new[] { "Sunny Days", "Sunny Days", "Sunny Days!!!", "???" };

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            for (var i = 0; i < locationIds.Length; i++)
            {
                await publicDb.Database.ExecuteSqlRawAsync($"""
                    INSERT INTO "{schemaName}"."locations"
                        ("Id", "Name", "Address", "Phone", "Email", "MaxCapacity", "FlexPermission",
                         "BoPermission", "CreatedAt", "UpdatedAt")
                    VALUES
                        ('{locationIds[i]}', '{names[i]}', 'Address', '+32 9 123 45 67',
                         '{Guid.NewGuid():N}@test.com', 15, false, false, now(), now());
                    """);
            }
        }

        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schemaName);
            await db.MigrateAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schemaName);

            var slugs = await db.Locations
                .Where(l => locationIds.Contains(l.Id))
                .Select(l => l.PublicEnrollmentSlug)
                .ToListAsync();

            Assert.Equal(4, slugs.Count);
            Assert.All(slugs, Assert.NotNull);
            Assert.All(slugs, s => Assert.False(string.IsNullOrWhiteSpace(s)));
            Assert.Equal(slugs.Count, slugs.Distinct().Count());

            // The three "Sunny Days"-family names collapse to the same base slug and must have
            // gotten disambiguating numeric suffixes rather than colliding.
            var sunnySlugs = slugs.Where(s => s!.StartsWith("sunny-days")).ToList();
            Assert.Equal(3, sunnySlugs.Count);
            Assert.Contains("sunny-days", sunnySlugs);
            Assert.Contains("sunny-days-2", sunnySlugs);
            Assert.Contains("sunny-days-3", sunnySlugs);

            // The punctuation-only name falls back to the "location" base slug (never empty).
            Assert.Contains("location", slugs);
        }
    }
}
