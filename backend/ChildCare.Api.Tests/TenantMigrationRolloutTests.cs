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

    /// <summary>Reverts a freshly-provisioned schema back to the pre-baseline-extension shape.</summary>
    private static async Task RevertToPreExtensionSchemaAsync(IServiceProvider services, string schemaName)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        await publicDb.Database.ExecuteSqlRawAsync($"""
            DROP TABLE "{schemaName}"."refresh_tokens";
            ALTER TABLE "{schemaName}"."users"
                DROP COLUMN "AppleId",
                DROP COLUMN "EmailVerificationExpiry",
                DROP COLUMN "EmailVerificationToken",
                DROP COLUMN "EmailVerified",
                DROP COLUMN "GoogleId",
                DROP COLUMN "PasswordResetExpiry",
                DROP COLUMN "PasswordResetToken";
            DELETE FROM "{schemaName}"."__EFMigrationsHistory" WHERE "MigrationId" LIKE '%ExtendUsersAddRefreshTokens';
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
