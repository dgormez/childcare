using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.MonthlyMenus;

/// <summary>
/// Feature 013e — monthly menu (director authoring + parent view) and meal-preference-change
/// requests. Covers spec.md's US1 (director creates/publishes) and US2 (parent views); US3-US5
/// live in MealPreferenceRequestTests.cs / later additions to this file.
/// </summary>
public class MonthlyMenuTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Monthly Menu Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        return (client, org, location);
    }

    private static Task<HttpResponseMessage> GetMenuRawAsync(HttpClient client, string token, Guid locationId, int year, int month) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{locationId}/monthly-menus/{year}/{month}", token));

    private static async Task<MonthlyMenuResponse> GetMenuAsync(HttpClient client, string token, Guid locationId, int year, int month)
    {
        var response = await GetMenuRawAsync(client, token, locationId, year, month);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MonthlyMenuResponse>())!;
    }

    private static Task<HttpResponseMessage> UpsertMenuRawAsync(
        HttpClient client, string token, Guid locationId, int year, int month, List<UpsertMonthlyMenuDayRequest> days) =>
        client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{locationId}/monthly-menus/{year}/{month}", token,
            new UpsertMonthlyMenuRequest(days)));

    private static async Task<MonthlyMenuResponse> UpsertMenuAsync(
        HttpClient client, string token, Guid locationId, int year, int month, List<UpsertMonthlyMenuDayRequest> days)
    {
        var response = await UpsertMenuRawAsync(client, token, locationId, year, month, days);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MonthlyMenuResponse>())!;
    }

    private static Task<HttpResponseMessage> PublishRawAsync(HttpClient client, string token, Guid locationId, int year, int month) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{locationId}/monthly-menus/{year}/{month}/publish", token));

    private static Task<HttpResponseMessage> UnpublishRawAsync(HttpClient client, string token, Guid locationId, int year, int month) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{locationId}/monthly-menus/{year}/{month}/unpublish", token));

    private static Task<HttpResponseMessage> GetParentMenuRawAsync(HttpClient client, string parentToken, int year, int month) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/monthly-menu?year={year}&month={month}", parentToken));

    private static async Task<List<ParentMonthlyMenuEntry>> GetParentMenuAsync(HttpClient client, string parentToken, int year, int month)
    {
        var response = await GetParentMenuRawAsync(client, parentToken, year, month);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ParentMonthlyMenuEntry>>())!;
    }

    private static async Task PublishAsync(HttpClient client, string token, Guid locationId, int year, int month)
    {
        var response = await PublishRawAsync(client, token, locationId, year, month);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<ClosureDayResponse> CreateAndPublishClosureAsync(HttpClient client, string accessToken, Guid locationId, DateOnly date)
    {
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/closures", accessToken,
            new CreateClosureDayRequest(locationId, date, "Sluitingsdag", "holiday", true)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        var publishResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closure.Id}/publish", accessToken, new PublishClosureDayRequest(false)));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        return closure;
    }

    // ── US1: director creates and publishes ────────────────────────────────────────────────────

    [Fact]
    public async Task Put_WhenNoMenuExists_CreatesDraft_AndSecondPutUpdatesRatherThanDuplicates()
    {
        var (client, org, location) = await SetupAsync();
        var days = new List<UpsertMonthlyMenuDayRequest>
        {
            new(new DateOnly(2027, 6, 1), "Tomatensoep", "Kip met puree", "Yoghurt", null),
        };

        var first = await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 6, days);
        Assert.True(first.Exists);
        Assert.False(first.IsPublished);
        Assert.Single(first.Days);

        var updatedDays = new List<UpsertMonthlyMenuDayRequest>
        {
            new(new DateOnly(2027, 6, 1), "Erwtensoep", "Vis met rijst", "Fruit", null),
            new(new DateOnly(2027, 6, 2), null, null, null, "Geen warme maaltijd"),
        };
        var second = await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 6, updatedDays);
        Assert.Equal(2, second.Days.Count);
        Assert.Equal("Erwtensoep", second.Days[0].Soup);

        // Still only one MonthlyMenu row for this location/year/month (FR-005) — verified via the
        // GET reflecting the second write's full replacement, not an accumulation of both writes.
        var fetched = await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 6);
        Assert.Equal(2, fetched.Days.Count);
    }

    [Fact]
    public async Task Publish_SetsPublishedAt_AndDirectorGetSeesRegardlessOfState()
    {
        var (client, org, location) = await SetupAsync();
        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 7,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 7, 1), "Soep", "Hoofdgerecht", "Dessert", null)]);

        var beforePublish = await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 7);
        Assert.False(beforePublish.IsPublished);

        var publishResponse = await PublishRawAsync(client, org.AccessToken, location.Id, 2027, 7);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var publishState = (await publishResponse.Content.ReadFromJsonAsync<MonthlyMenuPublishStateResponse>())!;
        Assert.True(publishState.IsPublished);
        Assert.NotNull(publishState.PublishedAt);

        var afterPublish = await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 7);
        Assert.True(afterPublish.IsPublished);
    }

    [Fact]
    public async Task PublishAndUnpublish_WhenNoMenuExists_Return404()
    {
        var (client, org, location) = await SetupAsync();

        var publishResponse = await PublishRawAsync(client, org.AccessToken, location.Id, 2027, 8);
        Assert.Equal(HttpStatusCode.NotFound, publishResponse.StatusCode);

        var unpublishResponse = await UnpublishRawAsync(client, org.AccessToken, location.Id, 2027, 8);
        Assert.Equal(HttpStatusCode.NotFound, unpublishResponse.StatusCode);
    }

    [Fact]
    public async Task DirectorEndpoints_AsNonDirector_Return403()
    {
        var (client, org, location) = await SetupAsync();
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var getResponse = await GetMenuRawAsync(client, parentToken, location.Id, 2027, 6);
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        var putResponse = await UpsertMenuRawAsync(client, parentToken, location.Id, 2027, 6, []);
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);

        var publishResponse = await PublishRawAsync(client, parentToken, location.Id, 2027, 6);
        Assert.Equal(HttpStatusCode.Forbidden, publishResponse.StatusCode);

        var unpublishResponse = await UnpublishRawAsync(client, parentToken, location.Id, 2027, 6);
        Assert.Equal(HttpStatusCode.Forbidden, unpublishResponse.StatusCode);
    }

    // ── US2: parent views the current month's published menu ───────────────────────────────────

    [Fact]
    public async Task GetParentMenu_ReturnsOneEntryPerActiveContractLocation_WithClosureDates()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);
        await CreateAndPublishClosureAsync(client, org.AccessToken, location.Id, new DateOnly(2027, 9, 21));

        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 9,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 9, 1), "Soep", "Hoofdgerecht", "Dessert", null)]);
        await PublishAsync(client, org.AccessToken, location.Id, 2027, 9);

        var entries = await GetParentMenuAsync(client, parentToken, 2027, 9);

        var entry = Assert.Single(entries);
        Assert.Equal(location.Id, entry.LocationId);
        Assert.True(entry.IsPublished);
        Assert.Single(entry.Days);
        Assert.Contains(new DateOnly(2027, 9, 21), entry.ClosureDates);
    }

    [Fact]
    public async Task GetParentMenu_WhenNoMenuPublishedForLocation_ReturnsUnpublishedEmptyEntry()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);

        var entries = await GetParentMenuAsync(client, parentToken, 2027, 10);

        var entry = Assert.Single(entries);
        Assert.False(entry.IsPublished);
        Assert.Empty(entry.Days);
    }

    [Fact]
    public async Task GetParentMenu_ChildWithContractsAtTwoLocations_ReturnsTwoDistinctEntries()
    {
        var (client, org, firstLocation) = await SetupAsync();
        var secondLocation = await CreateLocationAsync(client, org.AccessToken, "Second");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, firstLocation.Id, DayOfWeek.Monday);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, secondLocation.Id, DayOfWeek.Tuesday);

        var entries = await GetParentMenuAsync(client, parentToken, 2027, 11);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.LocationId == firstLocation.Id && e.LocationName == "Main");
        Assert.Contains(entries, e => e.LocationId == secondLocation.Id && e.LocationName == "Second");
    }

    [Fact]
    public async Task GetParentMenu_ForUnlinkedChildsLocation_NeverIncludesThatLocation()
    {
        var (client, org, location) = await SetupAsync();
        var (unlinkedChild, _, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, unlinkedChild.Id, location.Id, DayOfWeek.Monday);
        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 12,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 12, 1), "Soep", "Hoofdgerecht", "Dessert", null)]);
        await PublishAsync(client, org.AccessToken, location.Id, 2027, 12);

        var (_, _, otherParentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var entries = await GetParentMenuAsync(client, otherParentToken, 2027, 12);

        Assert.Empty(entries);
    }
}
