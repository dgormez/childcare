using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Parent;

/// <summary>
/// User Story 5 (FR-014): re-registering a push token overwrites the previously stored one —
/// a reinstall never leaves a stale token active. Observed as a black-box behavior via
/// FakeExpoPushSender rather than a direct DB read, mirroring MessageThreadEndpointsTests'
/// push-observation style.
/// </summary>
public class PushTokenTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T094/FR-014: PUT /api/parent/push-token overwrites any prior value ──

    [Fact]
    public async Task RegisterPushToken_Twice_OverwritesPriorValue()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PushToken Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var firstRegister = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[old]")));
        Assert.Equal(HttpStatusCode.OK, firstRegister.StatusCode);

        var secondRegister = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[new]")));
        Assert.Equal(HttpStatusCode.OK, secondRegister.StatusCode);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent/message-threads", parentToken, new CreateMessageThreadRequest(child.Id, "Overwrite test", "hi")));
        var thread = (await createResponse.Content.ReadFromJsonAsync<MessageThreadResponse>())!;

        var fakePushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/message-threads/{thread.Id}/messages", org.AccessToken, new SendMessageRequest("reply")));

        Assert.Contains(fakePushSender.Sent, p => p.PushToken == "ExponentPushToken[new]");
        Assert.DoesNotContain(fakePushSender.Sent, p => p.PushToken == "ExponentPushToken[old]");
    }
}
