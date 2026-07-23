using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 2 (spec.md AC1/AC3): GET /api/organisations/register/{token} pre-fills
/// the registration page's email for a valid invitation, and returns the same generic 404 for
/// an expired, revoked, or already-used token — never revealing which reason applies (FR-011,
/// research.md R5's posture extended to this new lookup).</summary>
public class GetInvitationInfoByTokenTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetInvitationInfo_ValidToken_ReturnsEmail()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        var invitedEmail = $"lookup_{Guid.NewGuid():N}@test.com";
        fakeEmailSender.OrganisationInvitationCalls.Clear();
        await client.SendAsync(KioskModeTestSupport.AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, null, null)));

        var registerUrl = fakeEmailSender.OrganisationInvitationCalls.Single().RegisterUrl;
        var token = QueryHelpers.ParseQuery(new Uri(registerUrl).Query)["token"].ToString();

        var response = await client.GetAsync($"/api/organisations/register/{Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = (await response.Content.ReadFromJsonAsync<InvitationInfoResponse>())!;
        Assert.Equal(invitedEmail, info.Email);
    }

    [Fact]
    public async Task GetInvitationInfo_UnknownToken_Returns404()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/organisations/register/not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvitationInfo_RevokedToken_Returns404()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        fakeEmailSender.OrganisationInvitationCalls.Clear();
        var createResponse = await client.SendAsync(KioskModeTestSupport.AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"revoked_lookup_{Guid.NewGuid():N}@test.com", null, null)));
        var invitation = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;
        await client.SendAsync(KioskModeTestSupport.AuthedRequest(HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/revoke", accessToken));

        var registerUrl = fakeEmailSender.OrganisationInvitationCalls.Single().RegisterUrl;
        var token = QueryHelpers.ParseQuery(new Uri(registerUrl).Query)["token"].ToString();

        var response = await client.GetAsync($"/api/organisations/register/{Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvitationInfo_ExpiredToken_Returns404()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        fakeEmailSender.OrganisationInvitationCalls.Clear();
        var createResponse = await client.SendAsync(KioskModeTestSupport.AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"expired_lookup_{Guid.NewGuid():N}@test.com", null, null)));
        var invitation = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            var entity = await db.Invitations.FirstAsync(i => i.Id == invitation.Id);
            entity.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var registerUrl = fakeEmailSender.OrganisationInvitationCalls.Single().RegisterUrl;
        var token = QueryHelpers.ParseQuery(new Uri(registerUrl).Query)["token"].ToString();

        var response = await client.GetAsync($"/api/organisations/register/{Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
