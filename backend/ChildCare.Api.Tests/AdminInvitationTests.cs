using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using Xunit;

namespace ChildCare.Api.Tests;

public class AdminInvitationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task CreateInvitation_WithMissingSuperAdminKey_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/invitations", new CreateInvitationRequest("someone@example.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // FR-002, FR-017
    }

    [Fact]
    public async Task CreateInvitation_WithIncorrectSuperAdminKey_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest("someone@example.com")),
        };
        request.Headers.Add("X-Superadmin-Key", "definitely-the-wrong-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_WithCorrectSuperAdminKey_Succeeds()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest("someone-else@example.com")),
        };
        request.Headers.Add("X-Superadmin-Key", OrganisationOnboardingWebAppFactory.SuperAdminApiKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
