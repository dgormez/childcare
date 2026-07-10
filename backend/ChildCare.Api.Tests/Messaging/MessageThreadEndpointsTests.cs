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

namespace ChildCare.Api.Tests.Messaging;

/// <summary>
/// User Story 2 (SC-002, SC-005, SC-006, SC-007): shared family message threads between a
/// parent and the KDV. Mirrors ParentInvitationEndpointsTests' setup pattern.
/// </summary>
public class MessageThreadEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static HttpRequestMessage ParentRequest(HttpMethod method, string url, string accessToken, object? body = null) =>
        AuthedRequest(method, url, accessToken, body);

    // ── T054: parent creates a child-scoped thread, visible to parent + participants ──

    [Fact]
    public async Task CreateThread_ChildScoped_VisibleToCreatingParent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Thread Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken,
            new CreateMessageThreadRequest(child.Id, "Medication question", "Can she have her 2pm dose today?")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var thread = (await response.Content.ReadFromJsonAsync<MessageThreadResponse>())!;
        Assert.Equal(child.Id, thread.ChildId);
        Assert.Single(thread.Messages);
        Assert.Equal("Can she have her 2pm dose today?", thread.Messages[0].Body);
    }

    // ── T055: two parent accounts for the same child share one thread (FR-003a) ────

    [Fact]
    public async Task CreateThread_TwoParentsForSameChild_ShareOneThread()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SharedThread Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentAToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken, firstName: "Mother");
        var parentBContact = await InviteAndLoginSecondParentForChildAsync(client, factory, org.Organisation.Slug, org.AccessToken, child.Id, firstName: "Father");
        var parentBLogin = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email = parentBContact.Email, password = "password123" });
        var parentBToken = (await parentBLogin.Content.ReadFromJsonAsync<AuthSessionResponse>())!.AccessToken;

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentAToken,
            new CreateMessageThreadRequest(child.Id, "Shared thread test", "Hello from mother")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var parentBView = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/message-threads/{thread.Id}", parentBToken));
        Assert.Equal(HttpStatusCode.OK, parentBView.StatusCode);
        var parentBThread = (await parentBView.Content.ReadFromJsonAsync<MessageThreadResponse>())!;
        Assert.Contains(parentBThread.Messages, m => m.Body == "Hello from mother");

        var listResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/message-threads", parentBToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<MessageThreadSummaryResponse>>())!;
        Assert.Contains(list, t => t.Id == thread.Id);
    }

    // ── T056: director reply appears in the same thread, chronological order ───────

    [Fact]
    public async Task DirectorReply_AppearsInSameThread_ChronologicalOrder()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reply Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken,
            new CreateMessageThreadRequest(child.Id, "Question", "First message")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var replyResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/message-threads/{thread.Id}/messages", org.AccessToken, new SendMessageRequest("Yes, already given at 2:05")));
        Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);

        var parentView = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/message-threads/{thread.Id}", parentToken));
        var parentThread = (await parentView.Content.ReadFromJsonAsync<MessageThreadResponse>())!;
        Assert.Equal(2, parentThread.Messages.Count);
        Assert.Equal("First message", parentThread.Messages[0].Body);
        Assert.Equal("Yes, already given at 2:05", parentThread.Messages[1].Body);
    }

    // ── T057: a non-participant parent is denied access ─────────────────────────────

    [Fact]
    public async Task GetThread_NotAParticipant_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NotParticipant Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentAToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var (_, _, parentBToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken, firstName: "Unrelated");

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentAToken,
            new CreateMessageThreadRequest(child.Id, "Private", "Only for parent A")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/message-threads/{thread.Id}", parentBToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.message_thread.not_participant", await response.Content.ReadAsStringAsync());
    }

    // ── T058: a general (non-child-specific) thread — creator only ──────────────────

    [Fact]
    public async Task CreateThread_General_AccessibleToCreatorOnly()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GeneralThread Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken,
            new CreateMessageThreadRequest(null, "General question", "What are your holiday hours?")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var thread = (await response.Content.ReadFromJsonAsync<MessageThreadResponse>())!;
        Assert.Null(thread.ChildId);
    }

    // ── T058a: a general thread is visible/replyable by any director/staff org-wide ──

    [Fact]
    public async Task GeneralThread_VisibleAndReplyableByDirector()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GeneralThreadStaff Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken,
            new CreateMessageThreadRequest(null, "General", "General question")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var directorView = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/message-threads/{thread.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, directorView.StatusCode);

        var replyResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/message-threads/{thread.Id}/messages", org.AccessToken, new SendMessageRequest("We're open all year")));
        Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);
    }

    // ── T059: thread list most-recently-active first, with unread indicator ────────

    [Fact]
    public async Task ListThreads_MostRecentlyActiveFirst_WithUnreadIndicator()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ThreadOrder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await CreateChildAsync(client, org.AccessToken, "SecondChild");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);

        var thread1Response = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child1.Id, "First thread", "msg1")));
        var thread1 = (await thread1Response.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var thread2Response = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child2.Id, "Second thread", "msg2")));
        var thread2 = (await thread2Response.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        // Reply on thread1 to make it the most recently active + carry an unread indicator.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/message-threads/{thread1.Id}/messages", org.AccessToken, new SendMessageRequest("reply")));

        var listResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/message-threads", parentToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<MessageThreadSummaryResponse>>())!;

        Assert.Equal(thread1.Id, list[0].Id);
        Assert.True(list.First(t => t.Id == thread1.Id).HasUnread);
        Assert.False(list.First(t => t.Id == thread2.Id).HasUnread);
    }

    // ── T059a: tenant isolation — director from tenant A cannot see tenant B's thread ──

    [Fact]
    public async Task GetThread_CrossTenant_DirectorCannotAccess()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"ThreadTenantA {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"ThreadTenantB {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var (childB, _, parentTokenB) = await InviteAndLoginParentAsync(client, factory, orgB.Organisation.Slug, orgB.AccessToken);

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentTokenB, new CreateMessageThreadRequest(childB.Id, "B's thread", "hello")));
        var threadB = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/message-threads/{threadB.Id}", orgA.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── T095/FR-012: a new director reply pushes to a parent with a valid token ─────

    [Fact]
    public async Task DirectorReply_ParentHasValidPushToken_TriggersPushSend()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PushMsg Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await client.SendAsync(ParentRequest(HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[test-msg]")));

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child.Id, "Push test", "hi")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var fakePushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/message-threads/{thread.Id}/messages", org.AccessToken, new SendMessageRequest("reply")));

        Assert.Contains(fakePushSender.Sent, p => p.PushToken == "ExponentPushToken[test-msg]");
    }

    // ── T096/FR-015: a push failure is logged, doesn't block the send, still creates in-app notification ──

    [Fact]
    public async Task DirectorReply_PushSendFails_DoesNotBlockSend_StillCreatesNotification()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PushFail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await client.SendAsync(ParentRequest(HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[test-fail]")));

        var createResponse = await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child.Id, "Push fail test", "hi")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var fakePushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        fakePushSender.ThrowOnSend = true;
        try
        {
            var replyResponse = await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/message-threads/{thread.Id}/messages", org.AccessToken, new SendMessageRequest("reply")));
            Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);
        }
        finally
        {
            fakePushSender.ThrowOnSend = false;
        }

        var notificationsResponse = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/notifications", parentToken));
        var notifications = (await notificationsResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>())!;
        Assert.Contains(notifications, n => n.Type == "newmessage");
    }

    // ── T097/FR-013: director list surfaces an unread-from-parent indicator ─────────

    [Fact]
    public async Task ListOrgThreads_SurfacesUnreadFromParentCount()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"UnreadIndicator Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        await client.SendAsync(ParentRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child.Id, "Unread test", "hi")));

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/message-threads", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<MessageThreadSummaryResponse>>())!;
        Assert.Contains(list, t => t.UnreadFromParentCount > 0);
    }
}
