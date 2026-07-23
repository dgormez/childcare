using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>/speckit-converge finding F1 (CRITICAL): FR-006/FR-010 require a revoked invitation
/// to be permanently unusable, but RegisterOrganisationCommandHandler never checked RevokedAt —
/// only the new GET pre-check endpoint did, which a direct POST call bypasses entirely. This
/// proves the fix at the actual write path, not just the pre-check.</summary>
public class RevokedInvitationCannotRegisterTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Register_WithRevokedToken_Returns404_EvenWhenCallingRegisterDirectly()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        var invitedEmail = $"revoked_register_{Guid.NewGuid():N}@test.com";
        fakeEmailSender.OrganisationInvitationCalls.Clear();
        var createResponse = await client.SendAsync(KioskModeTestSupport.AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, null, null)));
        var invitation = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;
        await client.SendAsync(KioskModeTestSupport.AuthedRequest(HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/revoke", accessToken));

        var registerUrl = fakeEmailSender.OrganisationInvitationCalls.Single().RegisterUrl;
        var token = QueryHelpers.ParseQuery(new Uri(registerUrl).Query)["token"].ToString();

        // Calling POST /api/organisations/register directly — skipping the GET pre-check
        // entirely, as a real bypass attempt would.
        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            token, $"Org {Guid.NewGuid():N}", "Director", invitedEmail, "password123"));

        Assert.Equal(HttpStatusCode.NotFound, registerResponse.StatusCode);
    }
}
