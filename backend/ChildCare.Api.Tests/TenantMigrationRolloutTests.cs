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
    /// the __EFMigrationsHistory row.
    /// </summary>
    private static async Task RevertToPreExtensionSchemaAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync($"""
            DROP TABLE "{schemaName}"."child_events";
            DROP TABLE "{schemaName}"."room_shifts";
            DROP TABLE "{schemaName}"."device_pairings";
            DROP TABLE "{schemaName}"."contracts";
            DROP TABLE "{schemaName}"."child_contacts";
            DROP TABLE "{schemaName}"."child_group_assignments";
            DROP TABLE "{schemaName}"."vaccination_records";
            DROP TABLE "{schemaName}"."groups";
            DROP TABLE "{schemaName}"."children";
            DROP TABLE "{schemaName}"."contacts";
            DROP TABLE "{schemaName}"."staff_location_eligibility";
            DROP TABLE "{schemaName}"."staff_invitations";
            DROP TABLE "{schemaName}"."staff_profiles";
            DROP TABLE "{schemaName}"."locations";
            DROP TABLE "{schemaName}"."refresh_tokens";
            ALTER TABLE "{schemaName}"."users"
                DROP COLUMN "AppleId",
                DROP COLUMN "EmailVerificationExpiry",
                DROP COLUMN "EmailVerificationToken",
                DROP COLUMN "EmailVerified",
                DROP COLUMN "GoogleId",
                DROP COLUMN "PasswordResetExpiry",
                DROP COLUMN "PasswordResetToken",
                DROP COLUMN "Role";
            DELETE FROM "{schemaName}"."__EFMigrationsHistory"
                WHERE "MigrationId" LIKE '%ExtendUsersAddRefreshTokens' OR "MigrationId" LIKE '%AddUserRole' OR "MigrationId" LIKE '%AddLocations' OR "MigrationId" LIKE '%AddStaff' OR "MigrationId" LIKE '%AddChildren' OR "MigrationId" LIKE '%AddContracts' OR "MigrationId" LIKE '%AddRoomShiftsAndDevicePairings' OR "MigrationId" LIKE '%AddChildEvents' OR "MigrationId" LIKE '%AddContactPushToken';
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
            _ = await db.RefreshTokens.CountAsync();
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
