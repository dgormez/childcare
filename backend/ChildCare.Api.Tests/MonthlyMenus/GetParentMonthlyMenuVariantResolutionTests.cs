using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.MonthlyMenus;

/// <summary>
/// Feature 013j — spec.md User Story 3 (parent automatically sees the right menu per child).
/// FR-008 (priority-order resolution, exact-type matching), FR-009 (fallback to base),
/// FR-010 (one entry per (location, child) pair).
/// </summary>
public class GetParentMonthlyMenuVariantResolutionTests(
    OrganisationOnboardingWebAppFactory factory,
    GetParentMonthlyMenuVariantResolutionTests.QueryCountingWebAppFactory countingFactory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>, IClassFixture<GetParentMonthlyMenuVariantResolutionTests.QueryCountingWebAppFactory>
{
    /// <summary>
    /// T039 — dedicated factory (own Postgres container) that swaps in a counting
    /// <see cref="ITenantDbContextResolver"/> so this file's batching regression test can assert
    /// SQL command count without adding interceptor plumbing to the shared
    /// <see cref="OrganisationOnboardingWebAppFactory"/> every other test class depends on.
    /// </summary>
    public class QueryCountingWebAppFactory : OrganisationOnboardingWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                // Registered after the base factory's real TenantDbContextResolver — last
                // registration wins for a non-collection service (same pattern this factory
                // already uses for FakeGoogleTokenValidator etc.).
                services.AddSingleton<ITenantDbContextResolver>(sp =>
                    new CountingTenantDbContextResolver(sp.GetRequiredService<IConfiguration>()));
            });
        }
    }

    private sealed class CountingTenantDbContextResolver(IConfiguration configuration) : ITenantDbContextResolver
    {
        public static int CommandCount;

        public ITenantDbContext ForSchema(string schemaName)
        {
            if (string.IsNullOrEmpty(schemaName))
                throw new InvalidOperationException(
                    "CountingTenantDbContextResolver.ForSchema was called with no schema name.");

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName));
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();
            optionsBuilder.AddInterceptors(new CommandCountingInterceptor());

            return new TenantDbContext(optionsBuilder.Options, schemaName);
        }
    }

    private sealed class CommandCountingInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            CountingTenantDbContextResolver.CommandCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupWithVariantsAsync(params string[] enabledVariants)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Menu Variant Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        if (enabledVariants.Length > 0)
        {
            var settingsResponse = await client.SendAsync(AuthedRequest(
                HttpMethod.Put, $"/api/locations/{location.Id}/menu-variant-settings", org.AccessToken,
                new UpdateLocationMenuVariantSettingsRequest(enabledVariants.ToList(), false)));
            Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        }
        return (client, org, location);
    }

    private static async Task SetDietaryTypeAsync(HttpClient client, string token, Guid childId, params string[] dietaryTypes)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/children/{childId}/meal-preferences", token,
            new UpsertMealPreferenceRequest(null, dietaryTypes.ToList(), null, null)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task UpsertAndPublishAsync(
        HttpClient client, string token, Guid locationId, int year, int month, string soup, string? variant = null)
    {
        var url = variant is null
            ? $"/api/locations/{locationId}/monthly-menus/{year}/{month}"
            : $"/api/locations/{locationId}/monthly-menus/{year}/{month}?variant={variant}";
        var upsertResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, url, token,
            new UpsertMonthlyMenuRequest([new UpsertMonthlyMenuDayRequest(new DateOnly(year, month, 1), soup, null, null, null)])));
        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        var publishUrl = variant is null
            ? $"/api/locations/{locationId}/monthly-menus/{year}/{month}/publish"
            : $"/api/locations/{locationId}/monthly-menus/{year}/{month}/publish?variant={variant}";
        var publishResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, publishUrl, token));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
    }

    private static async Task UpsertDraftOnlyAsync(HttpClient client, string token, Guid locationId, int year, int month, string soup, string variant)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{locationId}/monthly-menus/{year}/{month}?variant={variant}", token,
            new UpsertMonthlyMenuRequest([new UpsertMonthlyMenuDayRequest(new DateOnly(year, month, 1), soup, null, null, null)])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<List<ParentMonthlyMenuEntry>> GetParentMenuAsync(HttpClient client, string parentToken, int year, int month)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/monthly-menu?year={year}&month={month}", parentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ParentMonthlyMenuEntry>>())!;
    }

    [Fact]
    public async Task ChildWithNoDietaryType_AlwaysResolvesToBaseMenu()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 6, "Basis soep");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 6, "Veggie soep", "vegetarian");

        var entry = Assert.Single(await GetParentMenuAsync(client, parentToken, 2027, 6));

        Assert.Null(entry.ResolvedVariant);
        Assert.Equal("Basis soep", Assert.Single(entry.Days).Soup);
    }

    [Fact]
    public async Task ChildMatchingOnePublishedVariant_ResolvesToIt()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await SetDietaryTypeAsync(client, org.AccessToken, child.Id, "vegetarian");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 7, "Basis soep");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 7, "Veggie soep", "vegetarian");

        var entry = Assert.Single(await GetParentMenuAsync(client, parentToken, 2027, 7));

        Assert.Equal("vegetarian", entry.ResolvedVariant);
        Assert.Equal("Veggie soep", Assert.Single(entry.Days).Soup);
    }

    [Fact]
    public async Task ChildMatchingTwoVariants_ResolvesToHigherPriorityOne()
    {
        var (client, org, location) = await SetupWithVariantsAsync("halal", "vegetarian");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await SetDietaryTypeAsync(client, org.AccessToken, child.Id, "halal", "vegetarian");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 8, "Halal soep", "halal");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 8, "Veggie soep", "vegetarian");

        var entry = Assert.Single(await GetParentMenuAsync(client, parentToken, 2027, 8));

        Assert.Equal("halal", entry.ResolvedVariant);
        Assert.Equal("Halal soep", Assert.Single(entry.Days).Soup);
    }

    [Fact]
    public async Task ChildMatchingDraftOnlyVariant_FallsBackToNextOptionThenBase()
    {
        var (client, org, location) = await SetupWithVariantsAsync("halal", "vegetarian");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await SetDietaryTypeAsync(client, org.AccessToken, child.Id, "halal", "vegetarian");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 9, "Basis soep");
        await UpsertDraftOnlyAsync(client, org.AccessToken, location.Id, 2027, 9, "Halal draft", "halal");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 9, "Veggie soep", "vegetarian");

        var entry = Assert.Single(await GetParentMenuAsync(client, parentToken, 2027, 9));

        Assert.Equal("vegetarian", entry.ResolvedVariant);
        Assert.Equal("Veggie soep", Assert.Single(entry.Days).Soup);
    }

    [Fact]
    public async Task TwoChildrenAtSameLocation_ResolveIndependently()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian");
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child1.Id, location.Id, DayOfWeek.Monday);
        await CreateAndActivateContractAsync(client, org.AccessToken, child2.Id, location.Id, DayOfWeek.Monday);
        await SetDietaryTypeAsync(client, org.AccessToken, child1.Id, "vegetarian");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 10, "Basis soep");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 10, "Veggie soep", "vegetarian");

        var entries = await GetParentMenuAsync(client, parentToken, 2027, 10);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.ChildId == child1.Id && e.ResolvedVariant == "vegetarian");
        Assert.Contains(entries, e => e.ChildId == child2.Id && e.ResolvedVariant == null);
    }

    [Fact]
    public async Task VeganChild_DoesNotAutomaticallyMatchEnabledVegetarianVariant()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await SetDietaryTypeAsync(client, org.AccessToken, child.Id, "vegan");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 11, "Basis soep");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2027, 11, "Veggie soep", "vegetarian");

        var entry = Assert.Single(await GetParentMenuAsync(client, parentToken, 2027, 11));

        Assert.Null(entry.ResolvedVariant);
        Assert.Equal("Basis soep", Assert.Single(entry.Days).Soup);
    }

    [Fact]
    public async Task ChildAtTwoLocations_ResolvesIndependentlyAtEach()
    {
        var (client, org, firstLocation) = await SetupWithVariantsAsync("vegetarian");
        var secondLocation = await CreateLocationAsync(client, org.AccessToken, "Second");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, firstLocation.Id, DayOfWeek.Monday);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, secondLocation.Id, DayOfWeek.Tuesday);
        await SetDietaryTypeAsync(client, org.AccessToken, child.Id, "vegetarian");
        await UpsertAndPublishAsync(client, org.AccessToken, firstLocation.Id, 2027, 12, "Basis soep 1");
        await UpsertAndPublishAsync(client, org.AccessToken, firstLocation.Id, 2027, 12, "Veggie soep 1", "vegetarian");
        // Second location never enables vegetarian at all.
        await UpsertAndPublishAsync(client, org.AccessToken, secondLocation.Id, 2027, 12, "Basis soep 2");

        var entries = await GetParentMenuAsync(client, parentToken, 2027, 12);

        Assert.Equal(2, entries.Count);
        var firstEntry = Assert.Single(entries, e => e.LocationId == firstLocation.Id);
        var secondEntry = Assert.Single(entries, e => e.LocationId == secondLocation.Id);
        Assert.Equal("vegetarian", firstEntry.ResolvedVariant);
        Assert.Null(secondEntry.ResolvedVariant);
        Assert.Equal("Basis soep 2", Assert.Single(secondEntry.Days).Soup);
    }

    [Fact]
    public async Task ResolutionQueryCount_DoesNotScaleWithChildCount()
    {
        // T039 — research.md's efficiency decision: one MonthlyMenu fetch per location, not per
        // child. Uses the dedicated counting factory/tenant so this test's own setup queries
        // (registration, contracts, meal preferences) don't pollute the count.
        var client = countingFactory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Query Count Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var settingsResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/menu-variant-settings", org.AccessToken,
            new UpdateLocationMenuVariantSettingsRequest(["vegetarian"], false)));
        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, countingFactory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child1.Id, location.Id, DayOfWeek.Monday);
        await SetDietaryTypeAsync(client, org.AccessToken, child1.Id, "vegetarian");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2028, 3, "Basis soep");
        await UpsertAndPublishAsync(client, org.AccessToken, location.Id, 2028, 3, "Veggie soep", "vegetarian");

        CountingTenantDbContextResolver.CommandCount = 0;
        var oneChildEntries = await GetParentMenuAsync(client, parentToken, 2028, 3);
        var oneChildCommandCount = CountingTenantDbContextResolver.CommandCount;
        Assert.Single(oneChildEntries);

        var child2 = await CreateChildAsync(client, org.AccessToken, "Second");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child2.Id, location.Id, DayOfWeek.Monday);
        var child3 = await CreateChildAsync(client, org.AccessToken, "Third");
        await LinkContactAsync(client, org.AccessToken, child3.Id, contact.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, child3.Id, location.Id, DayOfWeek.Monday);

        CountingTenantDbContextResolver.CommandCount = 0;
        var threeChildEntries = await GetParentMenuAsync(client, parentToken, 2028, 3);
        var threeChildCommandCount = CountingTenantDbContextResolver.CommandCount;
        Assert.Equal(3, threeChildEntries.Count);

        // Three children at the same location must not triple the command count — the per-
        // location MonthlyMenu/closure fetches are shared across all of that location's children.
        Assert.Equal(oneChildCommandCount, threeChildCommandCount);
    }
}
