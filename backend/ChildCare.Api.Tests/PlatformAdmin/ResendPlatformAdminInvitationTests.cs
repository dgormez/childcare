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

/// <summary>User Story 3 (spec.md, tasks.md T023): POST .../resend creates a fresh invitation
/// with createdByEmail set from the caller's claims, marks the prior Revoked with
/// revokedByEmail/revokedAt also set from the caller's claims (FR-008 applies to resend too,
/// per /speckit-analyze finding F2); sends a new email; 409 if already Accepted; 404 if unknown.</summary>
public class ResendPlatformAdminInvitationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Resend_Pending_CreatesFreshInvitation_RevokesPrior_WithAttribution_SendsNewEmail()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        var invitedEmail = $"resend_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/platform-admin/invitations", accessToken,
            new CreatePlatformAdminInvitationRequest(invitedEmail, "Note", null)));
        var original = (await createResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        fakeEmailSender.OrganisationInvitationCalls.Clear();
        var resendResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{original.Id}/resend", accessToken));
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        var resent = (await resendResponse.Content.ReadFromJsonAsync<PlatformAdminInvitationResponse>())!;

        Assert.NotEqual(original.Id, resent.Id);
        Assert.Equal(invitedEmail, resent.Email);
        Assert.Equal("pending", resent.Status);
        Assert.NotNull(resent.CreatedByEmail);
        Assert.Single(fakeEmailSender.OrganisationInvitationCalls);

        var list = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform-admin/invitations", accessToken));
        var invitations = (await list.Content.ReadFromJsonAsync<List<PlatformAdminInvitationResponse>>())!;
        var originalAfter = invitations.Single(i => i.Id == original.Id);
        Assert.Equal("revoked", originalAfter.Status);
        Assert.NotNull(originalAfter.RevokedByEmail);
        Assert.NotNull(originalAfter.RevokedAt);
    }

    [Fact]
    public async Task Resend_AlreadyAccepted_Returns409()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");
        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();

        var invitedEmail = $"accept_resend_{Guid.NewGuid():N}@test.com";
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

        var resendResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{invitation.Id}/resend", accessToken));
        Assert.Equal(HttpStatusCode.Conflict, resendResponse.StatusCode);
    }

    [Fact]
    public async Task Resend_UnknownId_Returns404()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await RegisterPlatformAdminAsync(client, factory.Services, email: $"admin_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/platform-admin/invitations/{Guid.NewGuid()}/resend", accessToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
