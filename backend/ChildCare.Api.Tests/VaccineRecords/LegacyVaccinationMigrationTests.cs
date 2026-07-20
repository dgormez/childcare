using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>
/// Feature 013c (research.md R1, quickstart.md Scenario 6): the "AddVaccineAndHealthRecords"
/// migration backfills any pre-existing "vaccination_records" row (feature 006) into the new
/// "vaccine_records" schema before dropping the old table, rather than silently discarding real
/// tenant data. This test simulates a tenant that provisioned before this feature shipped: revert
/// just the one migration (restoring "vaccination_records", dropping the new tables), seed a
/// legacy row directly, then re-apply the migration and assert the row survived the move.
/// </summary>
public class LegacyVaccinationMigrationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest(email)),
        };
        request.Headers.Add("X-Superadmin-Key", OrganisationOnboardingWebAppFactory.SuperAdminApiKey);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;
    }

    private static async Task<RegisterOrganisationResponse> RegisterOrgAsync(HttpClient client, string orgName, string email)
    {
        var invitation = await CreateInvitationAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, orgName, $"{orgName} Director", email, "password123"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;
    }

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>
    /// Reverts the "AddVaccineAndHealthRecords" migration: drops the new tables, recreates
    /// "vaccination_records" (matching that migration's own Down() shape), and removes its
    /// __EFMigrationsHistory row — everything else this tenant already has stays untouched.
    /// Also reverts feature 006a's later "AddPediatricianContactToChild" migration (dropping the
    /// two columns it added to children, removing its history row too) — TenantDbContext's
    /// custom MigrateAsync() (research.md R8) computes "pending" as "from the schema's last
    /// *applied* migration to latest", so leaving a chronologically-later migration marked
    /// applied while this one is reverted would make that computed range empty (last applied ==
    /// latest == AddPediatricianContactToChild), and `MigrateAsync()` would silently no-op
    /// instead of restoring vaccine_records/health_records. This must be extended again for any
    /// future migration added after this one, for the same reason — feature 013d's
    /// "AddChildMealPreferences" is the next one, so its table is dropped and its history row
    /// removed here too (found by this test actually failing after that migration shipped, not
    /// by inspection). Feature 008b's "AddLocationRequiresCaregiverPin" only adds a column (no
    /// new table), so it's reverted via DROP COLUMN instead of DROP TABLE, same reasoning.
    /// Feature 013g's "AddVaccineCatalogAndAttachments" is the next one — since this test drops
    /// "vaccine_records" wholesale, that migration's three new columns on it (VaccineTypeId,
    /// CustomVaccineEntryId, AttachmentObjectPath) need no separate DROP COLUMN step, but its new
    /// tenant_custom_vaccine_entries table (referenced by vaccine_records) does need its own
    /// DROP TABLE, ordered after vaccine_records is already gone. Feature 013h's
    /// "AddIsPlatformAdminToUsers" is the next one — same shape as 008b's
    /// AddLocationRequiresCaregiverPin (one new column, no new table), so it's reverted via
    /// DROP COLUMN on "users" (found by this test actually failing after that migration
    /// shipped, same as 013d's note above predicted). Feature 013e's
    /// "AddMonthlyMenuAndMealPreferenceRequests" is the next one — three new tables
    /// (monthly_menu_days before monthly_menus, which it FKs to; meal_preference_change_requests
    /// independently), same DROP-TABLE-plus-history-removal shape as 013d's own note above.
    /// Feature 013j's "AddMonthlyMenuVariants" is the next one — its monthly_menus.Variant column
    /// needs no separate step since monthly_menus is already dropped wholesale above, but unlike
    /// that, locations is never dropped in this test (only ALTERed), so its new
    /// MenuVariantPriorityOrder column needs its own explicit DROP COLUMN, same as 008b's
    /// RequiresCaregiverPin right above it. Feature 014's "AddInvoices" is the next one — its
    /// invoices table (never referenced by anything this test also drops) needs its own DROP
    /// TABLE, and its three new locations columns (Erkenningsnummer, BankAccountNumber,
    /// InvoiceDueDays) need their own explicit DROP COLUMNs, same reason as
    /// MenuVariantPriorityOrder above. Feature 014a's
    /// "AddInvoiceRemindersAndLocationPaymentSettings" is the next one — its two new invoices
    /// columns (ReminderCount, LastReminderSentAt) need no separate step since invoices is
    /// already dropped wholesale above, but its three new locations columns
    /// (PaymentRemindersEnabled, PaymentReminderDelayDays, PaymentReminderCadenceDays) need
    /// their own explicit DROP COLUMNs, same reason as InvoiceDueDays above — confirms this
    /// pattern (012a, 013c, 006a, 013d, 013g, 014) applies to every tenant-schema migration,
    /// not just the ones usually named. Feature 015's "AddFiscalAttestations" is the next one —
    /// a new fiscal_attestations table (FKs to children and locations, neither of which this
    /// test drops, so no ordering concern) needs its own DROP TABLE, found by this test actually
    /// failing (MigrateAsync() silently no-op'd because the computed pending range was empty
    /// with AddFiscalAttestations left as the last-applied migration) rather than by inspection —
    /// same class of gap 013d's/013h's own notes above already predicted for "any future
    /// migration." Feature 016's "AddChildMilestoneObservations" is the next one after that —
    /// same recurring gap, this time with child_milestone_observations (FK to children only, no
    /// dependents) as the newly-added table. Feature 018's "AddGroupCapacity" is the next one —
    /// groups is never dropped in this test (only referenced), so its new nullable Capacity
    /// column needs its own explicit DROP COLUMN, same reason as MenuVariantPriorityOrder above;
    /// its "AddReportingIndexes" migration only adds/replaces indexes on invoices (already
    /// dropped wholesale above), so it needs no schema change, just its own history-removal
    /// entry, same reasoning as everything else in this note. Feature 020's
    /// "AddEmailCommunications" is the next one — its two new tables (bulk_email_recipients
    /// before bulk_email_sends, which it FKs to) get their own DROP TABLEs, and contacts is
    /// never dropped in this test (only referenced), so its new nullable DigestUnsubscribedAt
    /// column needs its own explicit DROP COLUMN, same reason as Capacity above (found by this
    /// test actually failing after that migration shipped, same as every migration-adding
    /// feature's note above predicted). Feature 030's "AddSiblingBillingSettingsAndFamilyGroupId"
    /// is the next one — its two new locations columns (SiblingDiscountPct,
    /// FamilyInvoiceBundlingEnabled) need their own explicit DROP COLUMNs, same reason as
    /// DigestUnsubscribedAt above; its new invoices column (FamilyGroupId) needs no separate
    /// step since invoices is already dropped wholesale above. Feature 021's
    /// "AddLocationQrCheckInEnabled" is the next one — its new locations column
    /// (QrCheckInEnabled) needs its own explicit DROP COLUMN, same reason as
    /// FamilyInvoiceBundlingEnabled above.
    /// </summary>
    private static async Task RevertToPreVaccineHealthRecordsAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync($"""
            DROP TABLE "{schemaName}"."bulk_email_recipients";
            DROP TABLE "{schemaName}"."bulk_email_sends";
            DROP TABLE "{schemaName}"."child_milestone_observations";
            DROP TABLE "{schemaName}"."fiscal_attestations";
            DROP TABLE "{schemaName}"."invoices";
            DROP TABLE "{schemaName}"."child_meal_preferences";
            DROP TABLE "{schemaName}"."meal_preference_change_requests";
            DROP TABLE "{schemaName}"."monthly_menu_days";
            DROP TABLE "{schemaName}"."monthly_menus";
            DROP TABLE "{schemaName}"."vaccine_records";
            DROP TABLE "{schemaName}"."tenant_custom_vaccine_entries";
            DROP TABLE "{schemaName}"."health_records";
            CREATE TABLE "{schemaName}"."vaccination_records" (
                "Id" uuid NOT NULL,
                "ChildId" uuid NOT NULL,
                "VaccineName" character varying(200) NOT NULL,
                "DateAdministered" date NOT NULL,
                "NextDueDate" date,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_vaccination_records" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_vaccination_records_children_ChildId" FOREIGN KEY ("ChildId")
                    REFERENCES "{schemaName}"."children" ("Id") ON DELETE CASCADE
            );
            ALTER TABLE "{schemaName}"."children"
                DROP COLUMN "PediatricianName",
                DROP COLUMN "PediatricianPhone";
            ALTER TABLE "{schemaName}"."locations"
                DROP COLUMN "RequiresCaregiverPin",
                DROP COLUMN "MenuVariantPriorityOrder",
                DROP COLUMN "Erkenningsnummer",
                DROP COLUMN "BankAccountNumber",
                DROP COLUMN "InvoiceDueDays",
                DROP COLUMN "PaymentRemindersEnabled",
                DROP COLUMN "PaymentReminderDelayDays",
                DROP COLUMN "PaymentReminderCadenceDays";
            ALTER TABLE "{schemaName}"."users"
                DROP COLUMN "IsPlatformAdmin";
            ALTER TABLE "{schemaName}"."groups"
                DROP COLUMN "Capacity";
            ALTER TABLE "{schemaName}"."contacts"
                DROP COLUMN "DigestUnsubscribedAt";
            ALTER TABLE "{schemaName}"."locations"
                DROP COLUMN "SiblingDiscountPct",
                DROP COLUMN "FamilyInvoiceBundlingEnabled";
            ALTER TABLE "{schemaName}"."locations"
                DROP COLUMN "QrCheckInEnabled";
            DELETE FROM "{schemaName}"."__EFMigrationsHistory"
                WHERE "MigrationId" LIKE '%AddVaccineAndHealthRecords' OR "MigrationId" LIKE '%AddPediatricianContactToChild' OR "MigrationId" LIKE '%AddChildMealPreferences' OR "MigrationId" LIKE '%AddLocationRequiresCaregiverPin' OR "MigrationId" LIKE '%AddVaccineCatalogAndAttachments' OR "MigrationId" LIKE '%AddIsPlatformAdminToUsers' OR "MigrationId" LIKE '%AddMonthlyMenuAndMealPreferenceRequests' OR "MigrationId" LIKE '%AddMonthlyMenuVariants' OR "MigrationId" LIKE '%AddInvoices' OR "MigrationId" LIKE '%AddInvoiceRemindersAndLocationPaymentSettings' OR "MigrationId" LIKE '%AddFiscalAttestations' OR "MigrationId" LIKE '%AddChildMilestoneObservations' OR "MigrationId" LIKE '%AddGroupCapacity' OR "MigrationId" LIKE '%AddReportingIndexes' OR "MigrationId" LIKE '%AddEmailCommunications' OR "MigrationId" LIKE '%AddSiblingBillingSettingsAndFamilyGroupId' OR "MigrationId" LIKE '%AddLocationQrCheckInEnabled';
            """);
    }

    [Fact]
    public async Task Migrate_BackfillsLegacyVaccinationRow_ThenDropsOldTable()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Legacy Vaccination Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schemaName = await GetSchemaNameAsync(org.Organisation.Id);

        var childResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null)));
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var child = (await childResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        await RevertToPreVaccineHealthRecordsAsync(factory.Services, schemaName);

        var legacyId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            await publicDb.Database.ExecuteSqlRawAsync($"""
                INSERT INTO "{schemaName}"."vaccination_records"
                    ("Id", "ChildId", "VaccineName", "DateAdministered", "NextDueDate", "CreatedAt")
                VALUES ('{legacyId}', '{child.Id}', 'DTP', '2026-01-15', '2026-07-15', '2026-01-15T09:00:00Z');
                """);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<Application.Common.ITenantDbContextResolver>();
            var db = resolver.ForSchema(schemaName);
            await db.MigrateAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<Application.Common.ITenantDbContextResolver>();
            var db = resolver.ForSchema(schemaName);

            var migrated = await db.VaccineRecords.SingleAsync(v => v.Id == legacyId);
            Assert.Equal(child.Id, migrated.ChildId);
            Assert.Equal("DTP", migrated.VaccineName);
            Assert.Equal(new DateOnly(2026, 1, 15), migrated.AdministeredOn);
            Assert.Equal(new DateOnly(2026, 7, 15), migrated.NextDueDate);
            Assert.Null(migrated.RecordedBy);
            Assert.Null(migrated.DeletedAt);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                publicDb.Database.ExecuteSqlRawAsync($"""SELECT 1 FROM "{schemaName}"."vaccination_records";"""));
        }
    }
}
