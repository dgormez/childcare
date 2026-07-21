using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 023, User Story 3 — tour invitation send, accept/decline response, terminal-status
/// guard, re-send/reschedule, and outcome recording (FR-015 through FR-018).
/// </summary>
public class TourInvitationTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateWaitingListEntryRequest RequestWithEmail(Guid locationId, string email) =>
        new("Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters", email, null, locationId, null, null);

    private static async Task<WaitingListEntryResponse> CreateEntryAsync(HttpClient client, string accessToken, Guid locationId, string email)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", accessToken, RequestWithEmail(locationId, email)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
    }

    private FakeEmailSender FakeEmailSender => (FakeEmailSender)factory.Services.GetRequiredService<IEmailSender>();

    // ── T041: send creates a tracked invitation, requires a contact email ─────────────

    [Fact]
    public async Task SendTourInvitation_WithContactEmail_SendsEmail_SetsStatusSent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Camellia");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id, "sophie@example.com");
        FakeEmailSender.TourInvitationCalls.Clear();

        var proposedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken, new SendTourInvitationRequest(proposedAt)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal("sent", updated.TourInvitationStatus);
        Assert.Equal(proposedAt, updated.TourProposedAt);
        Assert.NotNull(updated.TourInvitationSentAt);

        var call = Assert.Single(FakeEmailSender.TourInvitationCalls);
        Assert.Equal("sophie@example.com", call.ToEmail);
        Assert.Equal(proposedAt, call.ProposedAt);
    }

    [Fact]
    public async Task SendTourInvitation_NoContactEmail_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour NoEmail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Begonia");
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", org.AccessToken,
            new CreateWaitingListEntryRequest("Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters", null, null, location.Id, null, null)));
        var entry = (await createResponse.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken,
            new SendTourInvitationRequest(DateTime.UtcNow.AddDays(7))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── T042: accept/decline link records the response ──────────────────────────────

    [Theory]
    [InlineData("accepted", "accepted")]
    [InlineData("declined", "declined")]
    public async Task TourResponse_ValidToken_RecordsStatus(string response, string expectedStatus)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Response Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Dahlia");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id, "sophie@example.com");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken,
            new SendTourInvitationRequest(DateTime.UtcNow.AddDays(7))));

        var token = factory.Services.GetRequiredService<ITourInvitationTokenService>().CreateToken(entry.Id);
        var httpResponse = await client.GetAsync($"/api/public/enrollment/tour-response?token={Uri.EscapeDataString(token)}&org={org.Organisation.Slug}&response={response}");

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}&status=all", org.AccessToken));
        var updatedEntry = (await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!.Single(e => e.Id == entry.Id);
        Assert.Equal(expectedStatus, updatedEntry.TourInvitationStatus);
    }

    // ── T043: invalid/tampered token fails closed ────────────────────────────────────

    [Fact]
    public async Task TourResponse_TamperedToken_ReturnsCalmInvalidPage_WritesNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Tampered Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.GetAsync($"/api/public/enrollment/tour-response?token=not-a-real-token&org={org.Organisation.Slug}&response=accepted");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("niet (meer) geldig", body); // nl fallback InvalidLinkText — no raw error/stack trace
    }

    // ── Convergence T068: an expired token fails closed, same as a tampered one ───────
    // ── (spec.md's Testing Requirements explicitly lists "valid, expired, tampered") ──

    [Fact]
    public async Task TourResponse_ExpiredToken_ReturnsCalmInvalidPage_WritesNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Expired Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Foxglove");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id, "sophie@example.com");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken,
            new SendTourInvitationRequest(DateTime.UtcNow.AddDays(7))));

        // Same purpose string DataProtectionTourInvitationTokenService uses, but protected with
        // an already-elapsed lifetime — proves the real service's Unprotect call actually
        // enforces expiry (not just that a malformed string fails), without needing to wait out
        // the production 30-day window.
        var dataProtectionProvider = factory.Services.GetRequiredService<IDataProtectionProvider>();
        var expiredToken = dataProtectionProvider
            .CreateProtector("WaitingList.TourInvitation")
            .ToTimeLimitedDataProtector()
            .Protect(entry.Id.ToString(), TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var response = await client.GetAsync($"/api/public/enrollment/tour-response?token={Uri.EscapeDataString(expiredToken)}&org={org.Organisation.Slug}&response=accepted");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("niet (meer) geldig", body); // same generic invalid/expired page as a tampered token

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}&status=all", org.AccessToken));
        var updatedEntry = (await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!.Single(e => e.Id == entry.Id);
        Assert.Equal("sent", updatedEntry.TourInvitationStatus); // unchanged — the expired response was never applied
    }

    // ── T044: terminal-status guard ──────────────────────────────────────────────────

    [Fact]
    public async Task TourResponse_EntryAlreadyWithdrawn_ShowsNoLongerActive_DoesNotAlterStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Terminal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Elderflower");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id, "sophie@example.com");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken,
            new SendTourInvitationRequest(DateTime.UtcNow.AddDays(7))));
        var token = factory.Services.GetRequiredService<ITourInvitationTokenService>().CreateToken(entry.Id);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("withdrawn")));

        var response = await client.GetAsync($"/api/public/enrollment/tour-response?token={Uri.EscapeDataString(token)}&org={org.Organisation.Slug}&response=accepted");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}&status=all", org.AccessToken));
        var updatedEntry = (await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!.Single(e => e.Id == entry.Id);
        Assert.Equal("sent", updatedEntry.TourInvitationStatus); // unchanged from the send, never became "accepted"
    }

    // ── T045: outcome recording, independent of invitation status ───────────────────

    [Fact]
    public async Task RecordTourOutcome_SavesFreeText_IndependentOfInvitationStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Outcome Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Fennel");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id, "sophie@example.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-outcome", org.AccessToken,
            new RecordTourOutcomeRequest("Family visited, very positive")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal("Family visited, very positive", updated.TourOutcome);
        Assert.Equal("notSent", updated.TourInvitationStatus); // no invitation was ever sent
    }

    // ── T045a: re-sending overwrites the previous invitation, no history kept ───────

    [Fact]
    public async Task SendTourInvitation_Twice_OverwritesProposedTimeAndResetsStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Tour Resend Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Gardenia");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id, "sophie@example.com");

        var firstProposal = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken,
            new SendTourInvitationRequest(firstProposal)));
        var token = factory.Services.GetRequiredService<ITourInvitationTokenService>().CreateToken(entry.Id);
        await client.GetAsync($"/api/public/enrollment/tour-response?token={Uri.EscapeDataString(token)}&org={org.Organisation.Slug}&response=declined");

        var secondProposal = new DateTime(2026, 8, 22, 14, 0, 0, DateTimeKind.Utc);
        var secondResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/tour-invitation", org.AccessToken,
            new SendTourInvitationRequest(secondProposal)));

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var updated = (await secondResponse.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal(secondProposal, updated.TourProposedAt);
        Assert.Equal("sent", updated.TourInvitationStatus); // reset from "declined" back to "sent"
    }
}
