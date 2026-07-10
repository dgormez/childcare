using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Announcements;

/// <summary>User Story 3 (SC-004): director broadcasts a read-only announcement scoped to a location or group.</summary>
public class AnnouncementEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static HttpRequestMessage ParentRequest(HttpMethod method, string url, string accessToken, object? body = null) =>
        AuthedRequest(method, url, accessToken, body);

    private static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, new DateOnly(2023, 1, 1))));

    // ── T072: location-scoped announcement reaches every eligible contact ──────────

    [Fact]
    public async Task SendAnnouncement_LocationScoped_ReachesEligibleContact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Announce Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Closed Friday", "Staff training day")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var announcement = (await response.Content.ReadFromJsonAsync<AnnouncementResponse>())!;
        Assert.Equal(1, announcement.RecipientCount);

        var parentView = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/announcements/{announcement.Id}", parentToken));
        Assert.Equal(HttpStatusCode.OK, parentView.StatusCode);
    }

    // ── T072a/FR-008: a not-yet-invited contact is excluded, even though in scope ──

    [Fact]
    public async Task SendAnnouncement_ContactWithoutAccount_Excluded()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceNoAccount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Test", "Body")));

        var announcement = (await response.Content.ReadFromJsonAsync<AnnouncementResponse>())!;
        Assert.Equal(0, announcement.RecipientCount);
    }

    // ── T073: group-scoped announcement reaches only that group's contacts ─────────

    [Fact]
    public async Task SendAnnouncement_GroupScoped_ReachesOnlyThatGroup()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceGroup Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (childA, _, parentTokenA) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, groupA.Id);
        var (childB, _, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken, firstName: "GroupBParent");
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, groupB.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, groupA.Id, "Group A only", "Body")));

        var announcement = (await response.Content.ReadFromJsonAsync<AnnouncementResponse>())!;
        Assert.Equal(1, announcement.RecipientCount);

        var parentAView = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/announcements/{announcement.Id}", parentTokenA));
        Assert.Equal(HttpStatusCode.OK, parentAView.StatusCode);
    }

    // ── T074: zero-recipient scope completes without error ─────────────────────────

    [Fact]
    public async Task SendAnnouncement_ZeroRecipients_CompletesWithoutError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceEmpty Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Empty Location");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Test", "Body")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var announcement = (await response.Content.ReadFromJsonAsync<AnnouncementResponse>())!;
        Assert.Equal(0, announcement.RecipientCount);
    }

    // ── T074a/FR-012: announcement send dispatches a push to a recipient with a valid token ──

    [Fact]
    public async Task SendAnnouncement_RecipientHasPushToken_TriggersPushSend()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnouncePush Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);
        await client.SendAsync(ParentRequest(HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[test-announce]")));

        var fakePushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Push test", "Body")));

        Assert.Contains(fakePushSender.Sent, p => p.PushToken == "ExponentPushToken[test-announce]");
    }

    // ── T075: no endpoint allows a parent to reply to an announcement ──────────────

    [Fact]
    public async Task Announcement_NoReplyEndpointExists()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceNoReply Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        var sendResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, null, "Test", "Body")));
        var announcement = (await sendResponse.Content.ReadFromJsonAsync<AnnouncementResponse>())!;

        var replyAttempt = await client.SendAsync(ParentRequest(
            HttpMethod.Post, $"/api/parent/announcements/{announcement.Id}/messages", parentToken, new { body = "Can I reply?" }));
        Assert.Equal(HttpStatusCode.NotFound, replyAttempt.StatusCode);
    }

    // ── T076: a parent can only view an announcement they're a recipient of ────────

    [Fact]
    public async Task GetParentAnnouncement_NotARecipient_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceDenied Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentTokenA) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);
        var (_, _, parentTokenUnrelated) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken, firstName: "Unrelated");

        var sendResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken, new SendAnnouncementRequest(location.Id, group.Id, "Group only", "Body")));
        var announcement = (await sendResponse.Content.ReadFromJsonAsync<AnnouncementResponse>())!;

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/announcements/{announcement.Id}", parentTokenUnrelated));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.announcement.not_recipient", await response.Content.ReadAsStringAsync());
    }

    // ── T076a: tenant isolation — cross-tenant announcement never targetable/viewable ──

    [Fact]
    public async Task SendAnnouncement_CrossTenantLocation_Returns422()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"AnnounceTenantA {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"AnnounceTenantB {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var locationB = await CreateLocationAsync(client, orgB.AccessToken, "Location B");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", orgA.AccessToken, new SendAnnouncementRequest(locationB.Id, null, "Test", "Body")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetParentAnnouncement_CrossTenant_Returns403NotLeak()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"AnnounceViewTenantA {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"AnnounceViewTenantB {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var locationB = await CreateLocationAsync(client, orgB.AccessToken, "Location B");
        var groupB = await CreateGroupAsync(client, orgB.AccessToken, "Group B", locationB.Id);
        var (childB, _, _) = await InviteAndLoginParentAsync(client, factory, orgB.Organisation.Slug, orgB.AccessToken);
        await AssignChildToGroupAsync(client, orgB.AccessToken, childB.Id, groupB.Id);
        var sendResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", orgB.AccessToken, new SendAnnouncementRequest(locationB.Id, null, "Test", "Body")));
        var announcementB = (await sendResponse.Content.ReadFromJsonAsync<AnnouncementResponse>())!;

        var (_, _, parentTokenA) = await InviteAndLoginParentAsync(client, factory, orgA.Organisation.Slug, orgA.AccessToken);
        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/announcements/{announcementB.Id}", parentTokenA));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
