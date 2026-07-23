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
/// LegacyVaccinationMigrationTests.
///
/// AddDigitalEnrollment is no longer the newest tenant migration (024-esignature's
/// AddContractSigningAndSepaMandate, then 025's AddCodaPaymentMatching, then 026's
/// AddSepaDirectDebit, shipped after it) —
/// TenantDbContext.MigrateAsync() generates
/// its script from `applied.LastOrDefault()`, the *last recorded* migration, not the first gap;
/// if a later migration's history row is left in place while an earlier one's is deleted,
/// MigrateAsync() sees "already at the latest" and generates nothing, silently skipping the
/// reverted migration entirely. So every migration from AddDigitalEnrollment onward must be
/// deleted from history here, not just AddDigitalEnrollment alone — keep this list extended
/// whenever a new tenant migration ships (mirrors TenantMigrationRolloutTests' own list).
/// Feature 026's "AddSepaDirectDebit" is the next one after AddCodaPaymentMatching — unlike
/// TenantMigrationRolloutTests/LegacyVaccinationMigrationTests (which each drop `invoices`
/// wholesale at their own revert point), this test's revert point predates `invoices` even
/// existing in its dropped form here — `invoices` is untouched by this test's revert, so its
/// three new columns (SepaBatchId/SepaMandateReferenceUsed/SepaReturnReason) need their own
/// explicit DROP COLUMN, `contracts`' new SepaRevokedAt needs one alongside its other Sepa*
/// columns already dropped below, and the new sepa_batches table (FKs to locations, still
/// present here) needs its own DROP TABLE ordered before `invoices` is altered (invoices is the
/// table referencing it) — same FK-direction rule 013g's shipped-note first flagged. Found by
/// this test actually failing after AddSepaDirectDebit shipped, exactly as this comment already
/// predicted for "any future migration." Feature 027's "AddStaffAppPersonalRotaAndLeave" is
/// the next one - its new staff_leave_requests table (FK to staff_profiles, untouched here)
/// needs its own DROP TABLE; staff_schedules' six new columns (Status, CoverStaffId, Notes,
/// CreatedBy, IsPublished, PublishedAt) need explicit DROP COLUMNs plus IsAbsent added back
/// (same reasoning as LegacyVaccinationMigrationTests' equivalent note - the migration's own
/// Up() backfill step needs it present when re-applied), and staff_profiles' two new columns
/// (ContractedDays, PushToken) need their own explicit DROP COLUMNs too. Feature 028's
/// "AddStaffHrDossierAndTimeRegistration" is the next one — its two new tables
/// (staff_time_entries, staff_documents; both FK to staff_profiles, untouched here) each need
/// their own DROP TABLE, and staff_profiles' one new column (TimeEntryFunctions) needs its own
/// explicit DROP COLUMN. Found the exact failure this comment already predicted — this file's
/// own migration-history DELETE list still ended at AddStaffAppPersonalRotaAndLeave, so
/// MigrateAsync() silently no-op'd after AddStaffHrDossierAndTimeRegistration shipped, exactly
/// as described above.
/// </summary>
public class PublicEnrollmentSlugBackfillMigrationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task RevertToPreDigitalEnrollmentAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync($"""
            DROP TABLE "{schemaName}"."coda_transactions";
            DROP TABLE "{schemaName}"."coda_imports";
            ALTER TABLE "{schemaName}"."invoices"
                DROP COLUMN "SepaBatchId",
                DROP COLUMN "SepaMandateReferenceUsed",
                DROP COLUMN "SepaReturnReason";
            DROP TABLE "{schemaName}"."sepa_batches";
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
            ALTER TABLE "{schemaName}"."contracts"
                DROP COLUMN "SepaAuthorisedAt",
                DROP COLUMN "SepaIbanEncrypted",
                DROP COLUMN "SepaIbanLast4",
                DROP COLUMN "SepaMandateReference",
                DROP COLUMN "SepaRevokedAt",
                DROP COLUMN "SignatureData",
                DROP COLUMN "SignatureType",
                DROP COLUMN "SignedAt",
                DROP COLUMN "SignedByIp",
                DROP COLUMN "SigningToken",
                DROP COLUMN "SigningTokenExpiresAt";
            DROP TABLE "{schemaName}"."staff_leave_requests";
            ALTER TABLE "{schemaName}"."staff_schedules"
                DROP COLUMN "Status",
                DROP COLUMN "CoverStaffId",
                DROP COLUMN "Notes",
                DROP COLUMN "CreatedBy",
                DROP COLUMN "IsPublished",
                DROP COLUMN "PublishedAt";
            ALTER TABLE "{schemaName}"."staff_schedules"
                ADD COLUMN "IsAbsent" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE "{schemaName}"."staff_profiles"
                DROP COLUMN "ContractedDays",
                DROP COLUMN "PushToken";
            DROP TABLE "{schemaName}"."staff_time_entries";
            DROP TABLE "{schemaName}"."staff_documents";
            ALTER TABLE "{schemaName}"."staff_profiles"
                DROP COLUMN "TimeEntryFunctions";
            DELETE FROM "{schemaName}"."__EFMigrationsHistory"
                WHERE "MigrationId" LIKE '%AddDigitalEnrollment' OR "MigrationId" LIKE '%AddContractSigningAndSepaMandate'
                   OR "MigrationId" LIKE '%AddCodaPaymentMatching' OR "MigrationId" LIKE '%AddSepaDirectDebit'
                   OR "MigrationId" LIKE '%AddStaffAppPersonalRotaAndLeave' OR "MigrationId" LIKE '%AddStaffHrDossierAndTimeRegistration';
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
