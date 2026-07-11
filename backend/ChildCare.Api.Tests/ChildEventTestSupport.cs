using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>Shared HTTP/DB helpers for feature 009's child-events test suite.</summary>
internal static class ChildEventTestSupport
{
    public static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken, string firstName = "Emma") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    public static async Task<ContactResponse> CreateContactAsync(HttpClient client, string accessToken, string firstName = "Anna") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", "nl"))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    /// <summary>Links a contact to a child with CanPickup set, then directly seeds a PushToken
    /// on the underlying Contact row (data-model.md's correction — no registration UI exists
    /// yet, so tests seed it the same way RoomShiftTests reaches into the DB for setup that has
    /// no API surface).</summary>
    public static async Task<ContactResponse> CreatePickupEligibleContactWithPushTokenAsync(
        HttpClient client, IServiceProvider services, string accessToken, Guid childId, string schemaName,
        string pushToken = "ExponentPushToken[test]")
    {
        var contact = await CreateContactAsync(client, accessToken);
        var linkResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/contacts", accessToken,
            new LinkContactToChildRequest(contact.Id, "Mother", true, false)));
        Assert.Equal(System.Net.HttpStatusCode.Created, linkResponse.StatusCode);

        var db = ResolveTenantDb(services, schemaName);
        var row = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
        row.PushToken = pushToken;
        await db.SaveChangesAsync();

        return contact;
    }

    public static JsonElement ToPayload(object value) => JsonSerializer.SerializeToElement(value);

    public static Task<HttpResponseMessage> PostChildEventAsync(
        HttpClient client, string deviceToken, Guid childId, string eventType, DateTime occurredAt,
        object payload, DateTime? endedAt = null, bool visibleToParent = true, Guid? id = null, Guid? administeredByStaffId = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/child-events/", deviceToken, new
        {
            id,
            childId,
            eventType,
            occurredAt,
            endedAt,
            payload,
            visibleToParent,
            administeredByStaffId,
        }));

    public static Task<HttpResponseMessage> PatchChildEventAsDeviceAsync(
        HttpClient client, string deviceToken, Guid id, DateTime? endedAt = null, object? payload = null,
        bool? visibleToParent = null, Guid? administeredByStaffId = null) =>
        client.SendAsync(DeviceRequest(HttpMethod.Patch, $"/api/child-events/{id}", deviceToken, new
        {
            endedAt,
            payload,
            visibleToParent,
            administeredByStaffId,
        }));

    public static Task<HttpResponseMessage> PatchChildEventAsDirectorAsync(
        HttpClient client, string accessToken, Guid id, DateTime? endedAt = null, object? payload = null,
        bool? visibleToParent = null, Guid? administeredByStaffId = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/child-events/{id}", accessToken, new
        {
            endedAt,
            payload,
            visibleToParent,
            administeredByStaffId,
        }));

    public static Task<HttpResponseMessage> DeleteChildEventAsDeviceAsync(HttpClient client, string deviceToken, Guid id) =>
        client.SendAsync(DeviceRequest(HttpMethod.Delete, $"/api/child-events/{id}", deviceToken));

    public static Task<HttpResponseMessage> DeleteChildEventAsDirectorAsync(HttpClient client, string accessToken, Guid id) =>
        client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/child-events/{id}", accessToken));

    public static Task<HttpResponseMessage> GetChildEventsAsync(
        HttpClient client, string deviceToken, Guid childId, string? before = null, int? limit = null) =>
        client.SendAsync(DeviceRequest(
            HttpMethod.Get,
            $"/api/child-events?childId={childId}" + (before is null ? "" : $"&before={Uri.EscapeDataString(before)}") + (limit is null ? "" : $"&limit={limit}"),
            deviceToken));

    public static Task<HttpResponseMessage> GetDailySummaryAsync(HttpClient client, string deviceToken, Guid childId, DateOnly date) =>
        client.SendAsync(DeviceRequest(HttpMethod.Get, $"/api/child-events/daily-summary?childId={childId}&date={date:yyyy-MM-dd}", deviceToken));

    // Feature 009c — contracts/child-events-batch-api.md. `childIds` is a test-convenience
    // shorthand; each gets a fresh client-generated `id` unless the caller supplies its own
    // (childId, id) pairs via the `items` overload, used by the idempotent-retry test.
    public static Task<HttpResponseMessage> PostChildEventBatchAsync(
        HttpClient client, string deviceToken, IEnumerable<Guid> childIds, string eventType, DateTime occurredAt,
        object payload, DateTime? endedAt = null, bool visibleToParent = true) =>
        PostChildEventBatchAsync(
            client, deviceToken, childIds.Select(id => (id, Guid.NewGuid())), eventType, occurredAt, payload, endedAt, visibleToParent);

    public static Task<HttpResponseMessage> PostChildEventBatchAsync(
        HttpClient client, string deviceToken, IEnumerable<(Guid ChildId, Guid Id)> items, string eventType, DateTime occurredAt,
        object payload, DateTime? endedAt = null, bool visibleToParent = true) =>
        client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/child-events/batch", deviceToken, new
        {
            items = items.Select(i => new { childId = i.ChildId, id = i.Id }),
            eventType,
            occurredAt,
            endedAt,
            payload,
            visibleToParent,
        }));
}
