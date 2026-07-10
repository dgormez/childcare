using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Notifications;

/// <summary>User Story 4 (SC-006): a parent's single notification centre across message/announcement/temperature-alert types.</summary>
public class NotificationEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static HttpRequestMessage ParentRequest(HttpMethod method, string url, string accessToken, object? body = null) =>
        AuthedRequest(method, url, accessToken, body);

    // ── T084: lists message, announcement, and temperature-alert notifications, most-recent-first ──

    [Fact]
    public async Task ListNotifications_AllThreeTypes_MostRecentFirst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NotifCentre Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        // Temperature alert.
        await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow, new { celsius = 38.7 });

        // New message.
        var threadResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child.Id, "Q", "hi")));
        var thread = (await threadResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/message-threads/{thread.Id}/messages", org.AccessToken, new SendMessageRequest("reply")));

        // Announcement.
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Notice", "Body")));

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notifications = (await response.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;

        Assert.Contains(notifications, n => n.Type == "temperaturealert");
        Assert.Contains(notifications, n => n.Type == "newmessage");
        Assert.Contains(notifications, n => n.Type == "announcement");
        Assert.True(notifications.SequenceEqual(notifications.OrderByDescending(n => n.CreatedAt)));
    }

    // ── T085: marking one notification read doesn't affect another's read state ────

    [Fact]
    public async Task MarkRead_OneNotification_DoesNotAffectAnother()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"MarkRead Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "First", "Body")));
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Second", "Body")));

        var listResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentToken));
        var notifications = (await listResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        Assert.Equal(2, notifications.Count);

        var markResponse = await client.SendAsync(ParentRequest(HttpMethod.Post, $"/api/parent/notifications/{notifications[0].Id}/read", parentToken));
        Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);

        var afterResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        Assert.NotNull(after.First(n => n.Id == notifications[0].Id).ReadAt);
        Assert.Null(after.First(n => n.Id == notifications[1].Id).ReadAt);
    }

    // ── T086: a parent cannot mark or view another parent's notification ───────────

    [Fact]
    public async Task MarkRead_AnotherParentsNotification_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CrossParent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentAToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);
        var (_, _, parentBToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken, firstName: "Unrelated");

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Notice", "Body")));

        var listResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentAToken));
        var notifications = (await listResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        var notification = Assert.Single(notifications);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Post, $"/api/parent/notifications/{notification.Id}/read", parentBToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── T086a: tenant isolation — cross-tenant notification id never accessible ─────

    [Fact]
    public async Task MarkRead_CrossTenantNotificationId_Returns404NotLeak()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"NotifTenantA {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"NotifTenantB {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var locationB = await CreateLocationAsync(client, orgB.AccessToken, "Location B");
        var groupB = await CreateGroupAsync(client, orgB.AccessToken, "Group B", locationB.Id);
        var (childB, _, parentTokenB) = await InviteAndLoginParentAsync(client, factory, orgB.Organisation.Slug, orgB.AccessToken);
        await AssignChildToGroupAsync(client, orgB.AccessToken, childB.Id, groupB.Id);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", orgB.AccessToken, new SendAnnouncementRequest(locationB.Id, null, "Notice", "Body")));
        var listResponseB = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentTokenB));
        var notificationB = (await listResponseB.Content.ReadFromJsonAsync<List<NotificationResponse>>())!.Single();

        var (_, _, parentTokenA) = await InviteAndLoginParentAsync(client, factory, orgA.Organisation.Slug, orgA.AccessToken);
        var response = await client.SendAsync(ParentRequest(HttpMethod.Post, $"/api/parent/notifications/{notificationB.Id}/read", parentTokenA));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── T087: a temperature event over threshold creates a Notification row (feature 013 extension of feature 009) ──

    [Fact]
    public async Task TemperatureAlert_ParentHasAccount_CreatesInAppNotification()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"TempNotif Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow, new { celsius = 39.1 });

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentToken));
        var notifications = (await response.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        Assert.Contains(notifications, n => n.Type == "temperaturealert");
    }

    private static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, new DateOnly(2023, 1, 1))));
}
