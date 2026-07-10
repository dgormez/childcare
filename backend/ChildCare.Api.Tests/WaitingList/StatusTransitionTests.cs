using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 012a, User Story 3 — the status lifecycle: FR-007's explicit allow-list
/// (`waiting → offered/withdrawn`, `offered → enrolled/withdrawn/waiting`, nothing from
/// `enrolled`/`withdrawn`), and the FR-008/FR-009 email trigger which fires only on
/// `waiting → offered` and only when a contact email is on file.
/// </summary>
public class StatusTransitionTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateWaitingListEntryRequest Request(Guid locationId, string? contactEmail) =>
        new("Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters", contactEmail, null, locationId, null, null);

    private static async Task<WaitingListEntryResponse> CreateAsync(HttpClient client, string accessToken, Guid locationId, string? contactEmail = "sophie@example.com")
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", accessToken, Request(locationId, contactEmail)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
    }

    private static Task<HttpResponseMessage> TransitionRawAsync(HttpClient client, string accessToken, Guid id, string status) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{id}/status", accessToken, new TransitionWaitingListStatusRequest(status)));

    private FakeEmailSender FakeEmailSender => factory.Services.GetRequiredService<FakeEmailSender>();

    // ── T035/T036: email fires only with a contact email present ────────────────────────────

    [Fact]
    public async Task Offer_WithContactEmail_SendsNotification()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Status Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, location.Id, "sophie@example.com");

        var response = await TransitionRawAsync(client, org.AccessToken, entry.Id, "offered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(FakeEmailSender.WaitingListOfferedCalls, c => c.ToEmail == "sophie@example.com");
    }

    [Fact]
    public async Task Offer_WithoutContactEmail_SucceedsWithNoEmailAttempt()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Status Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, location.Id, null);
        var callsBefore = FakeEmailSender.WaitingListOfferedCalls.Count;

        var response = await TransitionRawAsync(client, org.AccessToken, entry.Id, "offered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(callsBefore, FakeEmailSender.WaitingListOfferedCalls.Count);
    }

    // ── T037: offered -> enrolled / offered -> withdrawn ─────────────────────────────────────

    [Theory]
    [InlineData("enrolled")]
    [InlineData("withdrawn")]
    public async Task Offered_TransitionsToEnrolledOrWithdrawn_Succeed(string targetStatus)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Status Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, location.Id);
        await TransitionRawAsync(client, org.AccessToken, entry.Id, "offered");

        var response = await TransitionRawAsync(client, org.AccessToken, entry.Id, targetStatus);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal(targetStatus, updated.Status);
    }

    // ── T038: offered -> waiting reverts, no email on the reverse transition ───────────────

    [Fact]
    public async Task Offered_RevertToWaiting_SendsNoEmailForReverseTransition()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Status Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, location.Id);
        await TransitionRawAsync(client, org.AccessToken, entry.Id, "offered");
        var callsAfterOffer = FakeEmailSender.WaitingListOfferedCalls.Count;

        var response = await TransitionRawAsync(client, org.AccessToken, entry.Id, "waiting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal("waiting", updated.Status);
        Assert.Equal(callsAfterOffer, FakeEmailSender.WaitingListOfferedCalls.Count);
    }

    // ── T039: any transition from enrolled/withdrawn rejected ───────────────────────────────

    [Theory]
    [InlineData("enrolled", "waiting")]
    [InlineData("enrolled", "offered")]
    [InlineData("withdrawn", "waiting")]
    [InlineData("withdrawn", "offered")]
    public async Task TerminalStatus_AnyTransition_Returns409(string fromStatus, string toStatus)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Status Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, location.Id);
        if (fromStatus == "enrolled")
        {
            await TransitionRawAsync(client, org.AccessToken, entry.Id, "offered");
            await TransitionRawAsync(client, org.AccessToken, entry.Id, "enrolled");
        }
        else
        {
            await TransitionRawAsync(client, org.AccessToken, entry.Id, "withdrawn");
        }

        var response = await TransitionRawAsync(client, org.AccessToken, entry.Id, toStatus);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.waiting_list.invalid_status_transition", await response.Content.ReadAsStringAsync());
    }

    // ── T040: waiting -> withdrawn directly, skipping offered ───────────────────────────────

    [Fact]
    public async Task Waiting_DirectlyToWithdrawn_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Status Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, location.Id);

        var response = await TransitionRawAsync(client, org.AccessToken, entry.Id, "withdrawn");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal("withdrawn", updated.Status);
    }
}
