using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.PlatformAdmin.PlatformAdminTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>User Story 3 (spec.md, tasks.md T024): POST .../revoke sets the revoke-attribution
/// fields from the caller's claims (never the request body); idempotent on an already-Revoked
/// invitation; 409 if already Accepted; 404 if unknown.</summary>
public class RevokePlatformAdminInvitationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Revoke_Pending_SetsAttributionFromCaller()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"revoke_{Guid.NewGuid():N}@test.com", null, null)));
        var invitation = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        var revokeResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/revoke", accessToken));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        var revoked = (await revokeResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        Assert.Equal("revoked", revoked.Status);
        Assert.NotNull(revoked.RevokedByEmail);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task Revoke_AlreadyRevoked_IsIdempotent()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest($"idempotent_{Guid.NewGuid():N}@test.com", null, null)));
        var invitation = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        var firstRevoke = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/revoke", accessToken));
        var firstResult = (await firstRevoke.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        var secondRevoke = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/revoke", accessToken));
        Assert.Equal(HttpStatusCode.OK, secondRevoke.StatusCode);
        var secondResult = (await secondRevoke.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        Assert.Equal(firstResult.RevokedAt, secondResult.RevokedAt);
        Assert.Equal(firstResult.RevokedByEmail, secondResult.RevokedByEmail);
    }

    [Fact]
    public async Task Revoke_AlreadyAccepted_Returns409()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        var invitedEmail = $"accept_revoke_{Guid.NewGuid():N}@test.com";
        fakeEmailSender.OrganisationInvitationCalls.Clear();
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, null, null)));
        var invitation = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        var registerUrl = fakeEmailSender.OrganisationInvitationCalls.Single().RegisterUrl;
        var token = QueryHelpers.ParseQuery(new Uri(registerUrl).Query)["token"].ToString();
        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            token, $"Org {Guid.NewGuid():N}", "Director", invitedEmail, "password123"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var revokeResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/revoke", accessToken));
        Assert.Equal(HttpStatusCode.Conflict, revokeResponse.StatusCode);
    }

    [Fact]
    public async Task Revoke_UnknownId_Returns404()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{Guid.NewGuid()}/revoke", accessToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
