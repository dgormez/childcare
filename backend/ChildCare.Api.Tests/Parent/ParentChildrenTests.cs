using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Parent;

/// <summary>User Story 1 (SC-007): GET /api/parent/children returns only the caller's own children.</summary>
public class ParentChildrenTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static HttpRequestMessage ParentRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task GetChildren_ReturnsOnlyOwnChildren()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentChildren Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (myChild, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild = await CreateChildAsync(client, org.AccessToken, "NotMyChild");

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/children", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var children = (await response.Content.ReadFromJsonAsync<List<ParentChildResponse>>())!;
        Assert.Contains(children, c => c.Id == myChild.Id);
        Assert.DoesNotContain(children, c => c.Id == otherChild.Id);
    }

    [Fact]
    public async Task GetChildren_TwoChildren_BothReturned()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentTwoChildren Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await CreateChildAsync(client, org.AccessToken, "SecondChild");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, "/api/parent/children", parentToken));

        var children = (await response.Content.ReadFromJsonAsync<List<ParentChildResponse>>())!;
        Assert.Contains(children, c => c.Id == child1.Id);
        Assert.Contains(children, c => c.Id == child2.Id);
    }
}
