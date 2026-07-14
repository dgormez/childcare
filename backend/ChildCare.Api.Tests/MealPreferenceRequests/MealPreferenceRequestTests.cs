using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.MealList.MealListTestSupport;

namespace ChildCare.Api.Tests.MealPreferenceRequests;

/// <summary>
/// Feature 013e — meal-preference-change requests. Covers spec.md's US3 (parent submits) and US4
/// (director reviews/decides).
/// </summary>
public class MealPreferenceRequestTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, RegisterOrganisationResponse Org)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Pref Request Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        return (client, org);
    }

    private static Task<HttpResponseMessage> SubmitRawAsync(
        HttpClient client, string parentToken, Guid childId, string? texture, List<string>? dietaryType, string? notes = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/parent/children/{childId}/meal-preference-requests", parentToken,
            new SubmitMealPreferenceChangeRequestRequest(texture, dietaryType, notes)));

    private static async Task<MealPreferenceChangeRequestResponse> SubmitAsync(
        HttpClient client, string parentToken, Guid childId, string? texture, List<string>? dietaryType, string? notes = null)
    {
        var response = await SubmitRawAsync(client, parentToken, childId, texture, dietaryType, notes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MealPreferenceChangeRequestResponse>())!;
    }

    private static Task<HttpResponseMessage> GetParentPreferenceRawAsync(HttpClient client, string parentToken, Guid childId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{childId}/meal-preference", parentToken));

    private static async Task<ParentMealPreferenceResponse> GetParentPreferenceAsync(HttpClient client, string parentToken, Guid childId)
    {
        var response = await GetParentPreferenceRawAsync(client, parentToken, childId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ParentMealPreferenceResponse>())!;
    }

    // ── US3: parent submits a request ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_ForLinkedChild_CreatesPendingRequest_AndLeavesMealPreferenceUnchanged()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var created = await SubmitAsync(client, parentToken, child.Id, "mixed", ["halal"], "Kan nu goed kauwen.");

        Assert.Equal("pending", created.Status);
        Assert.Equal("mixed", created.NewTexture);
        Assert.Equal(["halal"], created.NewDietaryType);

        var preferenceResponse = await GetMealPreferenceAsync(client, org.AccessToken, child.Id);
        Assert.Equal(HttpStatusCode.OK, preferenceResponse.StatusCode);
        var preference = (await preferenceResponse.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("normal", preference.Texture); // unchanged — no decision made yet
    }

    [Fact]
    public async Task Submit_SecondTimeWhilePending_Returns409()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await SubmitAsync(client, parentToken, child.Id, "mixed", null);

        var response = await SubmitRawAsync(client, parentToken, child.Id, "pieces", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("errors.meal_preference_requests.duplicate_pending", body!["errorKey"].ToString());
    }

    [Fact]
    public async Task Submit_ForUnlinkedChild_Returns403()
    {
        var (client, org) = await SetupAsync();
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);

        var response = await SubmitRawAsync(client, parentToken, otherChild.Id, "mixed", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetParentPreference_ReflectsPendingRequest_AndNullsForNoExistingPreference()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var before = await GetParentPreferenceAsync(client, parentToken, child.Id);
        Assert.Null(before.Texture);
        Assert.False(before.HasPendingRequest);

        await SubmitAsync(client, parentToken, child.Id, "mixed", null);

        var after = await GetParentPreferenceAsync(client, parentToken, child.Id);
        Assert.True(after.HasPendingRequest);
    }
}
