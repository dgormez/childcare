using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.MonthlyMenus;

/// <summary>
/// Feature 013j — spec.md User Story 2 (director authors and publishes a variant menu,
/// independently of the base menu and of every other variant).
/// </summary>
public class MonthlyMenuVariantAuthoringTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupWithVariantsAsync(params string[] enabledVariants)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Menu Variant Authoring Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
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

    private static string MenuUrl(Guid locationId, int year, int month, string? variant) =>
        variant is null
            ? $"/api/locations/{locationId}/monthly-menus/{year}/{month}"
            : $"/api/locations/{locationId}/monthly-menus/{year}/{month}?variant={variant}";

    private static Task<HttpResponseMessage> GetMenuRawAsync(HttpClient client, string token, Guid locationId, int year, int month, string? variant = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, MenuUrl(locationId, year, month, variant), token));

    private static async Task<MonthlyMenuResponse> GetMenuAsync(HttpClient client, string token, Guid locationId, int year, int month, string? variant = null)
    {
        var response = await GetMenuRawAsync(client, token, locationId, year, month, variant);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MonthlyMenuResponse>())!;
    }

    private static Task<HttpResponseMessage> UpsertMenuRawAsync(
        HttpClient client, string token, Guid locationId, int year, int month, List<UpsertMonthlyMenuDayRequest> days, string? variant = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Put, MenuUrl(locationId, year, month, variant), token, new UpsertMonthlyMenuRequest(days)));

    private static async Task<MonthlyMenuResponse> UpsertMenuAsync(
        HttpClient client, string token, Guid locationId, int year, int month, List<UpsertMonthlyMenuDayRequest> days, string? variant = null)
    {
        var response = await UpsertMenuRawAsync(client, token, locationId, year, month, days, variant);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MonthlyMenuResponse>())!;
    }

    private static string ActionUrl(Guid locationId, int year, int month, string action, string? variant) =>
        variant is null
            ? $"/api/locations/{locationId}/monthly-menus/{year}/{month}/{action}"
            : $"/api/locations/{locationId}/monthly-menus/{year}/{month}/{action}?variant={variant}";

    private static Task<HttpResponseMessage> PublishRawAsync(HttpClient client, string token, Guid locationId, int year, int month, string? variant = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, ActionUrl(locationId, year, month, "publish", variant), token));

    private static Task<HttpResponseMessage> UnpublishRawAsync(HttpClient client, string token, Guid locationId, int year, int month, string? variant = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, ActionUrl(locationId, year, month, "unpublish", variant), token));

    [Fact]
    public async Task UpsertAndPublish_ForVariant_NeverAffectsTheBaseMenu()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian");

        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 6,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 6, 1), "Basis soep", "Basis gerecht", "Basis dessert", null)]);

        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 6,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 6, 1), "Veggie soep", "Veggie gerecht", "Veggie dessert", null)],
            variant: "vegetarian");
        await PublishRawAsync(client, org.AccessToken, location.Id, 2027, 6, variant: "vegetarian");

        var baseMenu = await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 6);
        var variantMenu = await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 6, variant: "vegetarian");

        Assert.Equal("Basis soep", Assert.Single(baseMenu.Days).Soup);
        Assert.False(baseMenu.IsPublished);
        Assert.Equal("Veggie soep", Assert.Single(variantMenu.Days).Soup);
        Assert.True(variantMenu.IsPublished);
    }

    [Fact]
    public async Task UnpublishingVariant_NeverAffectsBaseOrOtherVariantsPublishState()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian", "halal");
        foreach (var variant in new string?[] { null, "vegetarian", "halal" })
        {
            await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 7,
                [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 7, 1), "Soep", "Gerecht", "Dessert", null)], variant);
            await PublishRawAsync(client, org.AccessToken, location.Id, 2027, 7, variant);
        }

        var unpublishResponse = await UnpublishRawAsync(client, org.AccessToken, location.Id, 2027, 7, variant: "vegetarian");
        Assert.Equal(HttpStatusCode.OK, unpublishResponse.StatusCode);

        Assert.True((await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 7)).IsPublished);
        Assert.False((await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 7, "vegetarian")).IsPublished);
        Assert.True((await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 7, "halal")).IsPublished);
    }

    [Fact]
    public async Task Upsert_ForNonEnabledVariant_Returns422()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian");

        var response = await UpsertMenuRawAsync(client, org.AccessToken, location.Id, 2027, 8,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 8, 1), "Soep", "Gerecht", "Dessert", null)],
            variant: "halal");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PublishAndUnpublish_ForNonEnabledVariant_Returns422()
    {
        var (client, org, location) = await SetupWithVariantsAsync();

        var publishResponse = await PublishRawAsync(client, org.AccessToken, location.Id, 2027, 9, variant: "halal");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, publishResponse.StatusCode);

        var unpublishResponse = await UnpublishRawAsync(client, org.AccessToken, location.Id, 2027, 9, variant: "halal");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unpublishResponse.StatusCode);
    }

    [Fact]
    public async Task DisablingThenReEnablingVariant_RetainsContentAndPublishState()
    {
        var (client, org, location) = await SetupWithVariantsAsync("halal");
        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 10,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 10, 1), "Halal soep", "Halal gerecht", "Halal dessert", null)], "halal");
        await PublishRawAsync(client, org.AccessToken, location.Id, 2027, 10, "halal");

        var disableResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/menu-variant-settings", org.AccessToken,
            new UpdateLocationMenuVariantSettingsRequest([], true)));
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        // While disabled, even a GET-adjacent write is rejected — content is retained, but the
        // variant is not currently writable/selectable (FR-007).
        var whileDisabled = await UpsertMenuRawAsync(client, org.AccessToken, location.Id, 2027, 10,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 10, 1), "Changed", "Changed", "Changed", null)], "halal");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, whileDisabled.StatusCode);

        var reEnableResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/menu-variant-settings", org.AccessToken,
            new UpdateLocationMenuVariantSettingsRequest(["halal"], false)));
        Assert.Equal(HttpStatusCode.OK, reEnableResponse.StatusCode);

        var afterReEnable = await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 10, "halal");
        Assert.Equal("Halal soep", Assert.Single(afterReEnable.Days).Soup);
        Assert.True(afterReEnable.IsPublished);
    }

    [Fact]
    public async Task UniqueIndex_AllowsOneBaseRowPlusOneRowPerDistinctVariant_ForSameLocationMonth()
    {
        var (client, org, location) = await SetupWithVariantsAsync("vegetarian", "halal");

        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 11,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 11, 1), "Basis", null, null, null)]);
        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 11,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 11, 1), "Veggie", null, null, null)], "vegetarian");
        await UpsertMenuAsync(client, org.AccessToken, location.Id, 2027, 11,
            [new UpsertMonthlyMenuDayRequest(new DateOnly(2027, 11, 1), "Halal", null, null, null)], "halal");

        Assert.Equal("Basis", Assert.Single((await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 11)).Days).Soup);
        Assert.Equal("Veggie", Assert.Single((await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 11, "vegetarian")).Days).Soup);
        Assert.Equal("Halal", Assert.Single((await GetMenuAsync(client, org.AccessToken, location.Id, 2027, 11, "halal")).Days).Soup);
    }
}
