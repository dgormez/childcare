using System.Net;
using System.Net.Http.Json;
using ChildCare.Api.Cli;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 3 (SC-003/SC-004): the migrate-tenants CLI subcommand rolls a pending structural
/// change out to every existing Ready tenant with no manual per-tenant step, and re-running is
/// a no-op (FR-010/FR-011, quickstart.md Scenario 3, contracts/migrate-tenants-cli.md).
///
/// Rather than defining a genuinely new, throwaway EF migration just for this test, one freshly
/// provisioned tenant's schema is reverted back to the pre-"ExtendUsersAddRefreshTokens" shape
/// (dropping the columns/table that migration added, and its __EFMigrationsHistory row) — this
/// deterministically simulates "a tenant that hasn't picked up the latest baseline yet" using
/// the migration that already exists, without depending on runtime schema/migration internals
/// beyond what this feature itself introduced.
/// </summary>
public class TenantMigrationRolloutTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<string> RegisterOrgAndGetSchemaAsync(HttpClient client, IServiceProvider services, string orgName, string email)
    {
        var invitation = await CreateInvitationAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, orgName, $"{orgName} Director", email, "password123"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = (await response.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;

        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == registered.Organisation.Id);
        return tenant.SchemaName;
    }

    /// <summary>
    /// Reverts a freshly-provisioned schema back to the pre-baseline-extension shape (i.e.
    /// immediately after "InitialTenantSchema", before anything else). Must undo *every*
    /// migration after that point, not just "ExtendUsersAddRefreshTokens" — feature 003's later
    /// "AddUserRole" migration means a revert that only deletes the
    /// ExtendUsersAddRefreshTokens history row (leaving AddUserRole's row and its Role column
    /// in place) fools EF's "last applied migration" check into thinking nothing is pending,
    /// since AddUserRole is chronologically the newest migration ID regardless of gaps before
    /// it — discovered when this test started failing after AddUserRole was introduced. Feature
    /// 004's "AddLocations" migration is the same class of gap and is reverted here for the same
    /// reason (discovered when this test started failing again after AddLocations shipped).
    /// Feature 005's "AddStaff" migration is the same class of gap again — its three new tables
    /// are dropped in FK-dependency order (staff_location_eligibility/staff_invitations depend
    /// on staff_profiles, which depends on users) before locations/users are touched, otherwise
    /// the statements below fail on a foreign-key constraint violation. Feature 006's
    /// "AddChildren" migration repeats the pattern again — child_contacts/
    /// child_group_assignments/vaccination_records depend on children/contacts/groups, and
    /// groups depends on locations, so all six new tables are dropped before locations/staff.
    /// Feature 007's "AddContracts" migration repeats the pattern once more — contracts has FKs
    /// to both children and locations (plus a self-FK), so it must be dropped before either.
    /// Feature 008a's "AddRoomShiftsAndDevicePairings" migration repeats it again — room_shifts
    /// has FKs to staff_profiles/locations/groups/device_pairings, and device_pairings has FKs
    /// to locations/groups/users, so both are dropped first, before anything they reference.
    /// Feature 009's "AddChildEvents" migration repeats it again — child_events has FKs to
    /// children/locations/groups/device_pairings, so it's dropped before all four. Its
    /// "AddContactPushToken" migration only adds a column to contacts, which the DROP TABLE
    /// "contacts" below already removes entirely — no separate revert step needed for it beyond
    /// the __EFMigrationsHistory row. Feature 010's "AddAttendanceRecords" migration repeats the
    /// pattern once more — attendance_records has FKs to children/locations, so it's dropped
    /// before either. Feature 011's "AddClosureCalendar" migration adds closure notification
    /// and message tables that depend on contacts and closure_days, plus attendance_records now
    /// depends on closure_days, so those closure tables are dropped before contacts. Feature
    /// 012's "AddStaffSchedules" migration repeats the pattern once more — staff_schedules has
    /// FKs to staff_profiles/locations/groups, so it's dropped before all three. Feature
    /// 012a's "AddWaitingListEntries" migration repeats it again — waiting_list_entries has
    /// FKs to both children and locations, so it's dropped before either. Feature 013's
    /// "AddParentCommunication" migration repeats the pattern once more — message_threads gains
    /// a new TenantUserId FK on contacts (dropped along with contacts itself, same as
    /// AddContactPushToken's column needing no separate step), plus six new tables:
    /// message_thread_participants/messages depend on message_threads and users;
    /// announcement_recipients depends on announcements and contacts; announcements depends on
    /// groups/locations/users; notifications depends on users; parent_invitations depends on
    /// contacts — all seven are dropped before contacts/groups/children/users are touched.
    /// Feature 009b's "AddGroupActivities" migration repeats the pattern once more —
    /// group_activity_photos depends on group_activities, which itself has FKs to
    /// groups/locations/device_pairings, so both are dropped before device_pairings/groups/
    /// locations. Feature 013a's "AddDayReservations" migration repeats the pattern once more —
    /// day_reservations has FKs to children and users; users is never dropped here (only
    /// ALTERed), so day_reservations only needs to precede the children drop, same as
    /// waiting_list_entries above it. Feature 013f's "AddLocationReservationSettings" adds four
    /// columns to the existing locations table (no new table, no new FK) — it still needs its
    /// own history-removal entry below, since dropping and recreating "locations" via a
    /// reapplied "AddLocations" would otherwise leave those four columns permanently missing
    /// while EF's migration history still claims the column-adding migration was applied.
    /// Feature 013b's "AddIncidentReports" migration repeats the pattern once more —
    /// incident_reports has FKs to both children and locations, so it's dropped before either.
    /// Feature 013c's "AddVaccineAndHealthRecords" migration replaces the "vaccination_records"
    /// drop line above with vaccine_records/health_records (both FK to children and, like
    /// day_reservations, to users — which is never dropped, only ALTERed — so they only need to
    /// precede the children drop). Feature 006a's "AddPediatricianContactToChild" migration is
    /// the same class of gap as 013f's "AddLocationReservationSettings" — it only adds two
    /// columns to the existing children table (no new table, no new FK), so it needs its own
    /// history-removal entry too, since dropping and recreating "children" via a reapplied
    /// "AddChildren" would otherwise leave those two columns permanently missing while EF's
    /// migration history still claims the column-adding migration was applied. Feature 013d's
    /// "AddChildMealPreferences" migration is the same class of gap as day_reservations/
    /// vaccine_records/health_records above it — child_meal_preferences has FKs to children and
    /// users (users is never dropped here, only ALTERed), so it only needs to precede the
    /// children drop. Feature 008b's "AddLocationRequiresCaregiverPin" migration is the same
    /// class of gap as 013f's "AddLocationReservationSettings" — it only adds one column to the
    /// existing locations table (no new table), so it needs its own history-removal entry too;
    /// no separate DROP COLUMN step is needed since the DROP TABLE "locations" below already
    /// removes the column along with the whole table. Feature 013g's
    /// "AddVaccineCatalogAndAttachments" migration repeats the FK-dependency-order pattern once
    /// more — tenant_custom_vaccine_entries has an FK from vaccine_records (not to it), so it
    /// must be dropped before vaccine_records; the migration's own three new columns on
    /// vaccine_records (VaccineTypeId, CustomVaccineEntryId, AttachmentObjectPath) need no
    /// separate DROP COLUMN step since the DROP TABLE "vaccine_records" below already removes
    /// them along with the whole table. Feature 013h's "AddIsPlatformAdminToUsers" migration is
    /// the same class of gap as 013f's "AddLocationReservationSettings"/006a's
    /// "AddPediatricianContactToChild" — it only adds one column to the existing users table (no
    /// new table, no new FK), so it needs its own history-removal entry too, and (unlike
    /// 008b's "AddLocationRequiresCaregiverPin", whose one new column is removed for free by the
    /// DROP TABLE "locations" above) users is never dropped here, only ALTERed, so its new
    /// IsPlatformAdmin column needs an explicit DROP COLUMN step alongside the existing ones.
    /// Feature 013e's "AddMonthlyMenuAndMealPreferenceRequests" migration adds three tables:
    /// monthly_menu_days must drop before monthly_menus (FK to it); meal_preference_change_requests
    /// has no FK to any table this method also drops other than children, so it only needs to
    /// precede that drop (research.md R7) — same recurring pattern every migration-adding feature
    /// since 012a has needed. Feature 013j's "AddMonthlyMenuVariants" migration is the same class
    /// of gap as 008b's "AddLocationRequiresCaregiverPin"/013f's "AddLocationReservationSettings"
    /// — it only adds columns to two already-dropped tables (monthly_menus.Variant,
    /// locations.MenuVariantPriorityOrder), so no separate DROP COLUMN step is needed, but it
    /// still needs its own history-removal entry. Feature 014's "AddInvoices" migration adds a
    /// new invoices table (FKs to children, contracts, and locations, so it must drop before all
    /// three) plus three columns on the already-dropped locations table (Erkenningsnummer,
    /// BankAccountNumber, InvoiceDueDays) that need no separate DROP COLUMN step for the same
    /// reason as 013j's MenuVariantPriorityOrder above. Feature 014a's
    /// "AddInvoiceRemindersAndLocationPaymentSettings" migration is the same class of gap once
    /// more — two columns on the already-dropped invoices table (ReminderCount,
    /// LastReminderSentAt) and three on the already-dropped locations table
    /// (PaymentRemindersEnabled, PaymentReminderDelayDays, PaymentReminderCadenceDays), no new
    /// table, so it only needs its own history-removal entry. Feature 015's "AddFiscalAttestations"
    /// migration adds a new fiscal_attestations table with FKs to children and locations, so it
    /// must drop before both — same recurring pattern every migration-adding feature since 012a
    /// has needed. Feature 016's "AddChildMilestoneObservations" migration adds a new
    /// child_milestone_observations table with an FK to children only, so it must drop before
    /// that table — same recurring pattern. Feature 018's "AddGroupCapacity" migration adds one
    /// nullable column to the already-dropped groups table, no new table, so it only needs its
    /// own history-removal entry (same as 014a's reminder-tracking columns above); its
    /// "AddReportingIndexes" migration only adds/replaces indexes on already-dropped tables, no
    /// column or table of its own, so it likewise only needs a history-removal entry. Feature
    /// 020's "AddEmailCommunications" migration adds one nullable column to the already-dropped
    /// contacts table (DigestUnsubscribedAt, no separate DROP COLUMN step needed for the same
    /// reason as 018's Group.Capacity above) plus two new tables (bulk_email_recipients has FKs
    /// to bulk_email_sends and contacts, so it must drop before both; bulk_email_sends has FKs to
    /// groups/locations/users, so it must drop before groups/locations) — same recurring pattern.
    /// Feature 030's "AddSiblingBillingSettingsAndFamilyGroupId" migration is the same class of
    /// gap once more — two columns on the already-dropped locations table (SiblingDiscountPct,
    /// FamilyInvoiceBundlingEnabled) and one on the already-dropped invoices table
    /// (FamilyGroupId), no new table, so it only needs its own history-removal entry.
    /// </summary>
    private static async Task RevertToPreExtensionSchemaAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync($"""
            DROP TABLE "{schemaName}"."day_reservations";
            DROP TABLE "{schemaName}"."waiting_list_entries";
            DROP TABLE "{schemaName}"."attendance_records";
            DROP TABLE "{schemaName}"."closure_notification_deliveries";
            DROP TABLE "{schemaName}"."parent_closure_messages";
            DROP TABLE "{schemaName}"."kdv_closure_days";
            DROP TABLE "{schemaName}"."staff_schedules";
            DROP TABLE "{schemaName}"."child_events";
            DROP TABLE "{schemaName}"."room_shifts";
            DROP TABLE "{schemaName}"."group_activity_photos";
            DROP TABLE "{schemaName}"."group_activities";
            DROP TABLE "{schemaName}"."device_pairings";
            DROP TABLE "{schemaName}"."child_milestone_observations";
            DROP TABLE "{schemaName}"."fiscal_attestations";
            DROP TABLE "{schemaName}"."invoices";
            DROP TABLE "{schemaName}"."contracts";
            DROP TABLE "{schemaName}"."child_contacts";
            DROP TABLE "{schemaName}"."child_group_assignments";
            DROP TABLE "{schemaName}"."vaccine_records";
            DROP TABLE "{schemaName}"."tenant_custom_vaccine_entries";
            DROP TABLE "{schemaName}"."health_records";
            DROP TABLE "{schemaName}"."message_thread_participants";
            DROP TABLE "{schemaName}"."messages";
            DROP TABLE "{schemaName}"."message_threads";
            DROP TABLE "{schemaName}"."announcement_recipients";
            DROP TABLE "{schemaName}"."announcements";
            DROP TABLE "{schemaName}"."bulk_email_recipients";
            DROP TABLE "{schemaName}"."bulk_email_sends";
            DROP TABLE "{schemaName}"."notifications";
            DROP TABLE "{schemaName}"."parent_invitations";
            DROP TABLE "{schemaName}"."incident_reports";
            DROP TABLE "{schemaName}"."child_meal_preferences";
            DROP TABLE "{schemaName}"."meal_preference_change_requests";
            DROP TABLE "{schemaName}"."groups";
            DROP TABLE "{schemaName}"."children";
            DROP TABLE "{schemaName}"."contacts";
            DROP TABLE "{schemaName}"."staff_location_eligibility";
            DROP TABLE "{schemaName}"."staff_invitations";
            DROP TABLE "{schemaName}"."staff_profiles";
            DROP TABLE "{schemaName}"."monthly_menu_days";
            DROP TABLE "{schemaName}"."monthly_menus";
            DROP TABLE "{schemaName}"."locations";
            DROP TABLE "{schemaName}"."refresh_tokens";
            ALTER TABLE "{schemaName}"."users"
                DROP COLUMN "AppleId",
                DROP COLUMN "EmailVerificationExpiry",
                DROP COLUMN "EmailVerificationToken",
                DROP COLUMN "EmailVerified",
                DROP COLUMN "GoogleId",
                DROP COLUMN "IsPlatformAdmin",
                DROP COLUMN "PasswordResetExpiry",
                DROP COLUMN "PasswordResetToken",
                DROP COLUMN "Role";
            DELETE FROM "{schemaName}"."__EFMigrationsHistory"
                WHERE "MigrationId" LIKE '%ExtendUsersAddRefreshTokens' OR "MigrationId" LIKE '%AddUserRole' OR "MigrationId" LIKE '%AddLocations' OR "MigrationId" LIKE '%AddStaff' OR "MigrationId" LIKE '%AddChildren' OR "MigrationId" LIKE '%AddContracts' OR "MigrationId" LIKE '%AddRoomShiftsAndDevicePairings' OR "MigrationId" LIKE '%AddChildEvents' OR "MigrationId" LIKE '%AddContactPushToken' OR "MigrationId" LIKE '%AddAttendanceRecords' OR "MigrationId" LIKE '%AddClosureCalendar' OR "MigrationId" LIKE '%AddStaffSchedules' OR "MigrationId" LIKE '%AddWaitingListEntries' OR "MigrationId" LIKE '%AddParentCommunication' OR "MigrationId" LIKE '%AddGroupActivities' OR "MigrationId" LIKE '%AddDayReservations' OR "MigrationId" LIKE '%AddLocationReservationSettings' OR "MigrationId" LIKE '%AddIncidentReports' OR "MigrationId" LIKE '%AddVaccineAndHealthRecords' OR "MigrationId" LIKE '%AddPediatricianContactToChild' OR "MigrationId" LIKE '%AddChildMealPreferences' OR "MigrationId" LIKE '%AddLocationRequiresCaregiverPin' OR "MigrationId" LIKE '%AddVaccineCatalogAndAttachments' OR "MigrationId" LIKE '%AddIsPlatformAdminToUsers' OR "MigrationId" LIKE '%AddMonthlyMenuAndMealPreferenceRequests' OR "MigrationId" LIKE '%AddMonthlyMenuVariants' OR "MigrationId" LIKE '%AddInvoices' OR "MigrationId" LIKE '%AddInvoiceRemindersAndLocationPaymentSettings' OR "MigrationId" LIKE '%AddFiscalAttestations' OR "MigrationId" LIKE '%AddChildMilestoneObservations' OR "MigrationId" LIKE '%AddGroupCapacity' OR "MigrationId" LIKE '%AddReportingIndexes' OR "MigrationId" LIKE '%AddEmailCommunications' OR "MigrationId" LIKE '%AddSiblingBillingSettingsAndFamilyGroupId';
            """);
    }

    private static async Task<bool> HasExtensionColumnsAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        // A round-trip query against the extended columns/table fails outright if they're
        // absent, rather than just returning an empty result — exactly what "reverted" means.
        try
        {
            _ = await db.Users.Select(u => u.GoogleId).FirstOrDefaultAsync();
            _ = await db.Users.Select(u => u.Role).FirstOrDefaultAsync();
            _ = await db.Users.Select(u => u.IsPlatformAdmin).FirstOrDefaultAsync();
            _ = await db.RefreshTokens.CountAsync();
            _ = await db.MonthlyMenus.CountAsync();
            _ = await db.MealPreferenceChangeRequests.CountAsync();
            _ = await db.Invoices.CountAsync();
            _ = await db.Locations.Select(l => l.InvoiceDueDays).FirstOrDefaultAsync();
            _ = await db.FiscalAttestations.CountAsync();
            _ = await db.ChildMilestoneObservations.CountAsync();
            _ = await db.Groups.Select(g => g.Capacity).FirstOrDefaultAsync();
            _ = await db.Contacts.Select(c => c.DigestUnsubscribedAt).FirstOrDefaultAsync();
            _ = await db.BulkEmailSends.CountAsync();
            _ = await db.BulkEmailRecipients.CountAsync();
            _ = await db.Locations.Select(l => l.SiblingDiscountPct).FirstOrDefaultAsync();
            _ = await db.Invoices.Select(i => i.FamilyGroupId).FirstOrDefaultAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── T034: rollout applies to every existing Ready tenant, no manual per-tenant step ─

    [Fact]
    public async Task MigrateTenants_AppliesPendingMigrationToEveryReadyTenant_NoManualStep()
    {
        var client = factory.CreateClient();

        var schemaBehind = await RegisterOrgAndGetSchemaAsync(client, factory.Services, "Rollout Org Behind", "director-rollout-behind@example.com");
        var schemaCurrent = await RegisterOrgAndGetSchemaAsync(client, factory.Services, "Rollout Org Current", "director-rollout-current@example.com");

        await RevertToPreExtensionSchemaAsync(factory.Services, schemaBehind);
        Assert.False(await HasExtensionColumnsAsync(factory.Services, schemaBehind));
        Assert.True(await HasExtensionColumnsAsync(factory.Services, schemaCurrent));

        int exitCode;
        using (var scope = factory.Services.CreateScope())
            exitCode = await MigrateTenantsCommand.RunAsync(scope.ServiceProvider);

        Assert.Equal(0, exitCode);
        Assert.True(await HasExtensionColumnsAsync(factory.Services, schemaBehind));  // rolled out — no manual step
        Assert.True(await HasExtensionColumnsAsync(factory.Services, schemaCurrent)); // untouched tenant unaffected
    }

    // ── T035: re-running against already-migrated tenants is a no-op ───────────────────

    [Fact]
    public async Task MigrateTenants_ReRunAfterEveryoneIsCurrent_IsNoOp()
    {
        var client = factory.CreateClient();

        var schema = await RegisterOrgAndGetSchemaAsync(client, factory.Services, "Rollout Org Idempotent", "director-rollout-idempotent@example.com");

        await RevertToPreExtensionSchemaAsync(factory.Services, schema);

        using (var scope = factory.Services.CreateScope())
            Assert.Equal(0, await MigrateTenantsCommand.RunAsync(scope.ServiceProvider));

        Assert.True(await HasExtensionColumnsAsync(factory.Services, schema));

        // Everyone is already current — re-running must not error or attempt to re-apply.
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(0, await MigrateTenantsCommand.RunAsync(scope.ServiceProvider));

        Assert.True(await HasExtensionColumnsAsync(factory.Services, schema));
    }
}
