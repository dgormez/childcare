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

    private static Task<HttpResponseMessage> ListRequestsRawAsync(HttpClient client, string directorToken, string? status = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, status is null ? "/api/meal-preference-requests" : $"/api/meal-preference-requests?status={status}", directorToken));

    private static async Task<List<MealPreferenceChangeRequestResponse>> ListRequestsAsync(HttpClient client, string directorToken, string? status = null)
    {
        var response = await ListRequestsRawAsync(client, directorToken, status);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MealPreferenceChangeRequestResponse>>())!;
    }

    private static Task<HttpResponseMessage> ApproveRawAsync(HttpClient client, string directorToken, Guid id) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/meal-preference-requests/{id}/approve", directorToken));

    private static Task<HttpResponseMessage> RejectRawAsync(HttpClient client, string directorToken, Guid id, string? reason = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/meal-preference-requests/{id}/reject", directorToken, new RejectMealPreferenceChangeRequestRequest(reason)));

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

    // ── US4: director reviews and decides ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListRequests_PairsPendingRequestsWithActiveHealthRecords()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateStandingMedicationAsync(client, org.AccessToken, child.Id, null, null);
        await SubmitAsync(client, parentToken, child.Id, "mixed", null);

        var requests = await ListRequestsAsync(client, org.AccessToken, "pending");

        var item = Assert.Single(requests);
        Assert.Equal(child.Id, item.ChildId);
        Assert.Single(item.ActiveHealthRecords);
    }

    [Fact]
    public async Task Approve_WritesThroughToMealPreference_MarksApproved_AndLeavesUntouchedFieldsAlone()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        // An existing MealPreference row with fields the request doesn't touch.
        await UpsertMealPreferenceAsync(client, org.AccessToken, child.Id, texture: "normal", dietaryType: ["halal"], portionSize: "large", additionalNotes: "Keep large portions");
        var created = await SubmitAsync(client, parentToken, child.Id, "pieces", null); // texture-only

        var approveResponse = await ApproveRawAsync(client, org.AccessToken, created.Id);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = (await approveResponse.Content.ReadFromJsonAsync<MealPreferenceChangeRequestResponse>())!;
        Assert.Equal("approved", approved.Status);
        Assert.NotNull(approved.DecidedAt);

        var preferenceResponse = await GetMealPreferenceAsync(client, org.AccessToken, child.Id);
        var preference = (await preferenceResponse.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("pieces", preference.Texture);
        Assert.Equal(["halal"], preference.DietaryType); // untouched by this texture-only request
        Assert.Equal("large", preference.PortionSize); // untouched
        Assert.Equal("Keep large portions", preference.AdditionalNotes); // untouched

        var secondApprove = await ApproveRawAsync(client, org.AccessToken, created.Id);
        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task Reject_LeavesMealPreferenceUnchanged_AndSetsDecisionNotesWhenReasonGiven()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var created = await SubmitAsync(client, parentToken, child.Id, "mixed", null);

        var rejectResponse = await RejectRawAsync(client, org.AccessToken, created.Id, "Nog niet nodig volgens de arts.");
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = (await rejectResponse.Content.ReadFromJsonAsync<MealPreferenceChangeRequestResponse>())!;
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("Nog niet nodig volgens de arts.", rejected.DecisionNotes);

        var preferenceResponse = await GetMealPreferenceAsync(client, org.AccessToken, child.Id);
        var preference = (await preferenceResponse.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("normal", preference.Texture); // unchanged — rejection never writes through

        var notificationsResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/notifications", parentToken));
        var notifications = (await notificationsResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        var notification = Assert.Single(notifications);
        Assert.Contains("with_note", notification.BodyKey);

        // A rejection with no reason must use the bare bodyKey, not interpolate a null value.
        var second = await SubmitAsync(client, parentToken, child.Id, "pieces", null);
        await RejectRawAsync(client, org.AccessToken, second.Id);
        var afterSecond = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/notifications", parentToken))).Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        var bareRejection = afterSecond.First(n => n.SourceId == second.Id);
        Assert.DoesNotContain("with_note", bareRejection.BodyKey);
    }

    [Fact]
    public async Task DirectorEndpoints_AsNonDirector_Return403()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var created = await SubmitAsync(client, parentToken, child.Id, "mixed", null);

        Assert.Equal(HttpStatusCode.Forbidden, (await ListRequestsRawAsync(client, parentToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ApproveRawAsync(client, parentToken, created.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RejectRawAsync(client, parentToken, created.Id)).StatusCode);
    }

    [Fact]
    public async Task Approve_ForDeactivatedChild_FailsCleanly_WithoutChangingMealPreferenceOrRequestState()
    {
        var (client, org) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var created = await SubmitAsync(client, parentToken, child.Id, "mixed", null);

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var approveResponse = await ApproveRawAsync(client, org.AccessToken, created.Id);
        Assert.Equal(HttpStatusCode.NotFound, approveResponse.StatusCode);

        var stillPending = await ListRequestsAsync(client, org.AccessToken, "pending");
        Assert.Contains(stillPending, r => r.Id == created.Id);
    }
}
