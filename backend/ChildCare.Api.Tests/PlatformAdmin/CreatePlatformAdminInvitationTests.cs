using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 1 (spec.md, tasks.md T009): POST /api/platform-admin/invitations creates
/// a Pending invitation with createdByEmail resolved server-side (never the request body,
/// research.md R12), defaults locale to "nl" when omitted, sends the invitation email via
/// IEmailSender; rejects an invalid email with 422; denied to a director without the flag.</summary>
public class CreatePlatformAdminInvitationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Create_PlatformAdmin_CreatesPendingInvitation_WithCreatorAttribution_SendsEmail()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();
        fakeEmailSender.OrganisationInvitationCalls.Clear();

        var invitedEmail = $"prospective_{Guid.NewGuid():N}@test.com";
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, "Zonnebloem KDV", null)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;
        Assert.Equal(invitedEmail, created.Email);
        Assert.Equal("pending", created.Status);
        Assert.Equal("nl", created.Locale);
        Assert.NotNull(created.CreatedByEmail);

        var call = Assert.Single(fakeEmailSender.OrganisationInvitationCalls);
        Assert.Equal(invitedEmail, call.ToEmail);
        Assert.Equal("nl", call.Locale);
        Assert.Contains("token=", call.RegisterUrl);
    }

    [Fact]
    public async Task Create_InvalidEmail_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest("not-an-email", null, null)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", org.AccessToken,
            new CreatePlatformAdminInvitationRequest("should-not-be-created@test.com", null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
