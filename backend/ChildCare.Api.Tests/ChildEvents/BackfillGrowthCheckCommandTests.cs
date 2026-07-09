using System.Net;
using System.Net.Http.Json;
using ChildCare.Api.Cli;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>
/// Feature 009a-child-events-custom-type, User Story 2: the backfill-growth-check CLI subcommand
/// rewrites every pre-existing `measurement` row to `growth_check` across every tenant schema,
/// preserving payload data, and is a no-op for a schema with none (research.md R1, tasks.md T016).
///
/// Seeding a "legacy `measurement` row" is done by recording a real event through the API (as
/// `growth_check`, the only value the API accepts post-rename) and then directly rewriting its
/// `event_type` column back to the literal string `measurement` via raw SQL — the only way to
/// get such a row into the table at all, since the running application code no longer
/// recognizes that wire value on any write path.
/// </summary>
public class BackfillGrowthCheckCommandTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<string> SchemaNameForAsync(IServiceProvider services, Guid tenantId)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    private static async Task RewriteEventTypeToLegacyMeasurementAsync(IServiceProvider services, string schemaName, Guid eventId)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        await publicDb.Database.ExecuteSqlRawAsync(
            $"""UPDATE "{schemaName}".child_events SET "EventType" = 'measurement' WHERE "Id" = '{eventId}'""");
    }

    [Fact]
    public async Task Backfill_RewritesLegacyMeasurementRows_AcrossMultipleTenants_PreservingPayload()
    {
        var client = factory.CreateClient();

        // Two independently-registered tenants, each with one growth-check event seeded, then
        // manually rewritten back to the legacy `measurement` wire value.
        var org1 = await RegisterOrgAsync(client, $"Backfill Org 1 {Guid.NewGuid():N}", $"director1_{Guid.NewGuid():N}@test.com");
        var location1 = await CreateLocationAsync(client, org1.AccessToken, "Location A");
        var group1 = await CreateGroupAsync(client, org1.AccessToken, "Group A", location1.Id);
        var child1 = await CreateChildAsync(client, org1.AccessToken);
        var (_, deviceToken1) = await PairDeviceAsync(client, org1.AccessToken, location1.Id, group1.Id);
        var create1 = await PostChildEventAsync(client, deviceToken1, child1.Id, "growth_check", DateTime.UtcNow, new { weightKg = 9.2 });
        Assert.Equal(HttpStatusCode.Created, create1.StatusCode);
        var event1 = (await create1.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var org2 = await RegisterOrgAsync(client, $"Backfill Org 2 {Guid.NewGuid():N}", $"director2_{Guid.NewGuid():N}@test.com");
        var location2 = await CreateLocationAsync(client, org2.AccessToken, "Location A");
        var group2 = await CreateGroupAsync(client, org2.AccessToken, "Group A", location2.Id);
        var child2 = await CreateChildAsync(client, org2.AccessToken);
        var (_, deviceToken2) = await PairDeviceAsync(client, org2.AccessToken, location2.Id, group2.Id);
        var create2 = await PostChildEventAsync(client, deviceToken2, child2.Id, "growth_check", DateTime.UtcNow, new { heightCm = 72.5 });
        Assert.Equal(HttpStatusCode.Created, create2.StatusCode);
        var event2 = (await create2.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var schema1 = await SchemaNameForAsync(factory.Services, org1.Organisation.Id);
        var schema2 = await SchemaNameForAsync(factory.Services, org2.Organisation.Id);
        await RewriteEventTypeToLegacyMeasurementAsync(factory.Services, schema1, event1.Id);
        await RewriteEventTypeToLegacyMeasurementAsync(factory.Services, schema2, event2.Id);

        int exitCode;
        using (var scope = factory.Services.CreateScope())
            exitCode = await BackfillGrowthCheckCommand.RunAsync(scope.ServiceProvider);
        Assert.Equal(0, exitCode);

        var events1 = await GetChildEventsAsync(client, deviceToken1, child1.Id);
        var body1 = (await events1.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        var found1 = Assert.Single(body1.Items, e => e.Id == event1.Id);
        Assert.Equal("growth_check", found1.EventType);
        Assert.Equal(9.2, found1.Payload.GetProperty("weightKg").GetDouble());

        var events2 = await GetChildEventsAsync(client, deviceToken2, child2.Id);
        var body2 = (await events2.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        var found2 = Assert.Single(body2.Items, e => e.Id == event2.Id);
        Assert.Equal("growth_check", found2.EventType);
        Assert.Equal(72.5, found2.Payload.GetProperty("heightCm").GetDouble());
    }

    [Fact]
    public async Task Backfill_TenantWithNoLegacyRows_CompletesAsNoOp()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Backfill NoOp Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var create = await PostChildEventAsync(client, deviceToken, child.Id, "growth_check", DateTime.UtcNow, new { weightKg = 5.0 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        int exitCode;
        using (var scope = factory.Services.CreateScope())
            exitCode = await BackfillGrowthCheckCommand.RunAsync(scope.ServiceProvider);
        Assert.Equal(0, exitCode);

        var events = await GetChildEventsAsync(client, deviceToken, child.Id);
        var body = (await events.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        var found = Assert.Single(body.Items, e => e.Id == created.Id);
        Assert.Equal("growth_check", found.EventType); // untouched — already the new value, never was `measurement`
    }
}
