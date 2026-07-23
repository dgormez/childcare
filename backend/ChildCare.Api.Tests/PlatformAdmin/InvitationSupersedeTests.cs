using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 1 (spec.md AC2, tasks.md T010): creating a second invitation for an email
/// with an existing Pending/Expired invitation marks the prior one Revoked (attributed to the
/// acting platform-admin, research.md R3) and only the new one is usable.</summary>
public class InvitationSupersedeTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Create_DuplicateEmail_SupersedesPriorInvitation()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var invitedEmail = $"jane_{Guid.NewGuid():N}@test.com";

        var firstResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, null, null)));
        var first = (await firstResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        var secondResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, null, null)));
        var second = (await secondResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        var list = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/invitations", accessToken));
        var invitations = (await list.Content.ReadFromJsonAsync<List<PlatformAdminInvitationResponse>>())!;

        var firstAfter = invitations.Single(i => i.Id == first.Id);
        var secondAfter = invitations.Single(i => i.Id == second.Id);

        Assert.Equal("revoked", firstAfter.Status);
        Assert.NotNull(firstAfter.RevokedByEmail);
        Assert.NotNull(firstAfter.RevokedAt);
        Assert.Equal("pending", secondAfter.Status);
    }
}
