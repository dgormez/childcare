using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.MealList;

/// <summary>Shared HTTP helpers for feature 013d's meal-list test suite.</summary>
internal static class MealListTestSupport
{
    public static async Task<ChildResponse> CreateChildWithAllergySeverityAsync(
        HttpClient client, string accessToken, string? allergySeverity, string firstName = "Emma") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, allergySeverity, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    public static Task<HttpResponseMessage> AssignChildToGroupAsync(
        HttpClient client, string accessToken, Guid childId, Guid groupId, DateOnly startDate) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, startDate)));

    public static Task<HttpResponseMessage> CreateStandingMedicationAsync(
        HttpClient client, string accessToken, Guid childId, DateOnly? validFrom, DateOnly? validUntil) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/health-records", accessToken,
            new CreateHealthRecordRequest("medication_standing", "Antibiotics", "Twice daily.", validFrom, validUntil)));

    public static HttpRequestMessage MealListRequest(
        string bearerToken, bool isDevice, Guid locationId, DateOnly date, bool? includeExpected = null)
    {
        var url = $"/api/locations/{locationId}/meal-list?date={date:yyyy-MM-dd}"
            + (includeExpected is null ? "" : $"&includeExpected={includeExpected.Value.ToString().ToLowerInvariant()}");
        return isDevice ? DeviceRequest(HttpMethod.Get, url, bearerToken) : AuthedRequest(HttpMethod.Get, url, bearerToken);
    }

    public static Task<HttpResponseMessage> GetMealPreferenceAsync(HttpClient client, string accessToken, Guid childId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childId}/meal-preferences", accessToken));

    public static Task<HttpResponseMessage> UpsertMealPreferenceAsync(
        HttpClient client, string accessToken, Guid childId,
        string? texture = null, List<string>? dietaryType = null, string? portionSize = null, string? additionalNotes = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{childId}/meal-preferences", accessToken,
            new UpsertMealPreferenceRequest(texture, dietaryType, portionSize, additionalNotes)));
}
