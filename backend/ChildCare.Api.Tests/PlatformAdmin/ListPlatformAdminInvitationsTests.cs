using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 3 (spec.md, tasks.md T022): GET /api/platform-admin/invitations returns
/// the correct derived status (Pending/Accepted/Expired/Revoked) per data-model.md's derivation
/// rules; a director without the flag gets 403.</summary>
public class ListPlatformAdminInvitationsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task List_ReturnsCorrectDerivedStatus_ForEachCase()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        // Pending
        var pendingResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"pending_{Guid.NewGuid():N}@test.com", null, null)));
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        // Revoked
        var revokedCreate = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"revoked_{Guid.NewGuid():N}@test.com", null, null)));
        var revokedInvitation = (await revokedCreate.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/platform-admin/invitations/{revokedInvitation.Id}/revoke", accessToken));

        // Expired — created via the API, then backdated directly (no API surface bumps ExpiresAt backward)
        var expiredCreate = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"expired_{Guid.NewGuid():N}@test.com", null, null)));
        var expiredInvitation = (await expiredCreate.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            var entity = await db.Invitations.FirstAsync(i => i.Id == expiredInvitation.Id);
            entity.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        // Accepted — extract the real token from the captured email (never exposed via the API response)
        var acceptedEmail = $"accepted_{Guid.NewGuid():N}@test.com";
        fakeEmailSender.OrganisationInvitationCalls.Clear();
        var acceptedCreate = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(acceptedEmail, null, null)));
        var acceptedInvitation = (await acceptedCreate.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;
        var registerUrl = fakeEmailSender.OrganisationInvitationCalls.Single().RegisterUrl;
        var token = QueryHelpers.ParseQuery(new Uri(registerUrl).Query)["token"].ToString();
        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            token, $"Accepted Org {Guid.NewGuid():N}", "Accepted Director", acceptedEmail, "password123"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var list = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/invitations", accessToken));
        var invitations = (await list.Content.ReadFromJsonAsync<List<PlatformAdminInvitationResponse>>())!;

        Assert.Equal("pending", invitations.Single(i => i.Id == pending.Id).Status);
        Assert.Equal("revoked", invitations.Single(i => i.Id == revokedInvitation.Id).Status);
        Assert.Equal("expired", invitations.Single(i => i.Id == expiredInvitation.Id).Status);
        Assert.Equal("accepted", invitations.Single(i => i.Id == acceptedInvitation.Id).Status);
    }

    [Fact]
    public async Task List_DirectorWithoutFlag_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/invitations", org.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
