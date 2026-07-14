using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.MonthlyMenus;

/// <summary>
/// Feature 013j — spec.md User Story 1 (director configures which variants a location offers)
/// and FR-014's removal-warning safety check.
/// </summary>
public class MonthlyMenuVariantSettingsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Menu Variant Settings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        return (client, org, location);
    }

    private static Task<HttpResponseMessage> PutSettingsRawAsync(
        HttpClient client, string token, Guid locationId, List<string> order, bool confirm = false) =>
        client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{locationId}/menu-variant-settings", token,
            new UpdateLocationMenuVariantSettingsRequest(order, confirm)));

    private static async Task<LocationResponse> PutSettingsAsync(HttpClient client, string token, Guid locationId, List<string> order, bool confirm = false)
    {
        var response = await PutSettingsRawAsync(client, token, locationId, order, confirm);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
    }

    private static Task<HttpResponseMessage> GetLocationRawAsync(HttpClient client, string token, Guid locationId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{locationId}", token));

    private static async Task<LocationResponse> GetLocationAsync(HttpClient client, string token, Guid locationId)
    {
        var response = await GetLocationRawAsync(client, token, locationId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
    }

    [Fact]
    public async Task NewLocation_HasNoVariantsEnabledByDefault()
    {
        var (client, org, location) = await SetupAsync();

        var fetched = await GetLocationAsync(client, org.AccessToken, location.Id);

        Assert.Empty(fetched.MenuVariantPriorityOrder);
        Assert.Empty(fetched.MenuVariantsWithPublishedContent);
    }

    [Fact]
    public async Task PutSettings_PersistsOrder_AndLeavesOtherLocationsUnchanged()
    {
        var (client, org, location) = await SetupAsync();
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");

        var updated = await PutSettingsAsync(client, org.AccessToken, location.Id, ["halal", "vegetarian"]);

        Assert.Equal(["halal", "vegetarian"], updated.MenuVariantPriorityOrder);

        var refetched = await GetLocationAsync(client, org.AccessToken, location.Id);
        Assert.Equal(["halal", "vegetarian"], refetched.MenuVariantPriorityOrder);

        var otherFetched = await GetLocationAsync(client, org.AccessToken, otherLocation.Id);
        Assert.Empty(otherFetched.MenuVariantPriorityOrder);
    }

    [Fact]
    public async Task PutSettings_WithDuplicateOrUnrecognizedDietaryType_Returns422()
    {
        var (client, org, location) = await SetupAsync();

        var duplicateResponse = await PutSettingsRawAsync(client, org.AccessToken, location.Id, ["halal", "halal"]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicateResponse.StatusCode);

        var unrecognizedResponse = await PutSettingsRawAsync(client, org.AccessToken, location.Id, ["not-a-diet"]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unrecognizedResponse.StatusCode);
    }

    [Fact]
    public async Task PutSettings_AsNonDirector_Returns403()
    {
        var (client, org, location) = await SetupAsync();
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await PutSettingsRawAsync(client, parentToken, location.Id, ["halal"]);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReEnablingRemovedVariant_AppendsAtEnd_NotPriorPosition()
    {
        var (client, org, location) = await SetupAsync();
        await PutSettingsAsync(client, org.AccessToken, location.Id, ["halal", "vegetarian"]);

        await PutSettingsAsync(client, org.AccessToken, location.Id, ["vegetarian"]);

        var reEnabled = await PutSettingsAsync(client, org.AccessToken, location.Id, ["vegetarian", "halal"]);

        Assert.Equal(["vegetarian", "halal"], reEnabled.MenuVariantPriorityOrder);
    }

    [Fact]
    public async Task GetLocation_ReturnsMenuVariantsWithPublishedContent_OnlyForCurrentOrFutureMonths()
    {
        var (client, org, location) = await SetupAsync();
        await PutSettingsAsync(client, org.AccessToken, location.Id, ["halal", "vegetarian"]);

        var future = DateTime.UtcNow.AddMonths(2);
        await UpsertVariantMenuAsync(client, org.AccessToken, location.Id, future.Year, future.Month, "halal");
        await PublishVariantAsync(client, org.AccessToken, location.Id, future.Year, future.Month, "halal");
        // Vegetarian gets a draft only — must NOT appear in menuVariantsWithPublishedContent.
        await UpsertVariantMenuAsync(client, org.AccessToken, location.Id, future.Year, future.Month, "vegetarian");

        var fetched = await GetLocationAsync(client, org.AccessToken, location.Id);

        Assert.Equal(["halal"], fetched.MenuVariantsWithPublishedContent);
    }

    [Fact]
    public async Task RemovingAVariantWithPublishedContent_RequiresConfirmation()
    {
        var (client, org, location) = await SetupAsync();
        await PutSettingsAsync(client, org.AccessToken, location.Id, ["halal"]);
        var future = DateTime.UtcNow.AddMonths(1);
        await UpsertVariantMenuAsync(client, org.AccessToken, location.Id, future.Year, future.Month, "halal");
        await PublishVariantAsync(client, org.AccessToken, location.Id, future.Year, future.Month, "halal");

        var withoutConfirm = await PutSettingsRawAsync(client, org.AccessToken, location.Id, []);
        Assert.Equal(HttpStatusCode.Conflict, withoutConfirm.StatusCode);

        var withConfirm = await PutSettingsAsync(client, org.AccessToken, location.Id, [], confirm: true);
        Assert.Empty(withConfirm.MenuVariantPriorityOrder);
    }

    [Fact]
    public async Task RemovingAVariantWithNoPublishedContent_NeedsNoConfirmation()
    {
        var (client, org, location) = await SetupAsync();
        await PutSettingsAsync(client, org.AccessToken, location.Id, ["halal"]);

        var response = await PutSettingsRawAsync(client, org.AccessToken, location.Id, []);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> UpsertVariantMenuAsync(
        HttpClient client, string token, Guid locationId, int year, int month, string variant) =>
        client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{locationId}/monthly-menus/{year}/{month}?variant={variant}", token,
            new UpsertMonthlyMenuRequest([new UpsertMonthlyMenuDayRequest(new DateOnly(year, month, 1), "Soep", "Hoofdgerecht", "Dessert", null)])));

    private static async Task PublishVariantAsync(HttpClient client, string token, Guid locationId, int year, int month, string variant)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/monthly-menus/{year}/{month}/publish?variant={variant}", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
