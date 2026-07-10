using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 012a, User Story 2 — director reorders the priority queue: per-location scoping
/// (FR-006), up/down moves (FR-005), and the `waiting`-only restriction (FR-005, research.md R6).
/// </summary>
public class ReorderTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateWaitingListEntryRequest Request(Guid locationId, string firstName) =>
        new(firstName, "Test", new DateOnly(2025, 3, 10), "Contact Name", null, null, locationId, null, null);

    private static async Task<WaitingListEntryResponse> CreateAsync(HttpClient client, string accessToken, Guid locationId, string firstName)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", accessToken, Request(locationId, firstName)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
    }

    private static Task<HttpResponseMessage> ReorderRawAsync(HttpClient client, string accessToken, Guid id, string direction) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{id}/reorder", accessToken, new ReorderWaitingListEntryRequest(direction)));

    private static async Task<List<WaitingListEntryResponse>> ListAsync(HttpClient client, string accessToken, Guid locationId)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={locationId}", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;
    }

    // ── T028: reorder up/down updates priority, list re-sorts ───────────────────────────────

    [Fact]
    public async Task Reorder_Down_MovesEntryBelowItsNeighbor()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reorder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var first = await CreateAsync(client, org.AccessToken, location.Id, "Emma");
        var second = await CreateAsync(client, org.AccessToken, location.Id, "Louis");

        var response = await ReorderRawAsync(client, org.AccessToken, first.Id, "down");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = (await response.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;

        Assert.True(entries.Single(e => e.Id == first.Id).Priority > entries.Single(e => e.Id == second.Id).Priority);
    }

    [Fact]
    public async Task Reorder_Up_MovesEntryAboveItsNeighbor()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reorder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var first = await CreateAsync(client, org.AccessToken, location.Id, "Emma");
        var second = await CreateAsync(client, org.AccessToken, location.Id, "Louis");

        var response = await ReorderRawAsync(client, org.AccessToken, second.Id, "up");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = (await response.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;

        Assert.True(entries.Single(e => e.Id == second.Id).Priority < entries.Single(e => e.Id == first.Id).Priority);
    }

    [Fact]
    public async Task Reorder_AtTopBoundary_Returns400()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reorder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var first = await CreateAsync(client, org.AccessToken, location.Id, "Emma");

        var response = await ReorderRawAsync(client, org.AccessToken, first.Id, "up");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── T029: per-location scoping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Reorder_InOneLocation_DoesNotAffectAnotherLocationsQueue()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reorder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "B");
        var a1 = await CreateAsync(client, org.AccessToken, locationA.Id, "Emma");
        var a2 = await CreateAsync(client, org.AccessToken, locationA.Id, "Louis");
        var b1 = await CreateAsync(client, org.AccessToken, locationB.Id, "Noor");
        var b2 = await CreateAsync(client, org.AccessToken, locationB.Id, "Jan");
        var beforeB = await ListAsync(client, org.AccessToken, locationB.Id);

        await ReorderRawAsync(client, org.AccessToken, a1.Id, "down");

        var afterB = await ListAsync(client, org.AccessToken, locationB.Id);
        Assert.Equal(
            beforeB.OrderBy(e => e.Priority).Select(e => e.Id),
            afterB.OrderBy(e => e.Priority).Select(e => e.Id));
    }

    // ── T030: not reorderable outside `waiting` ──────────────────────────────────────────────

    [Theory]
    [InlineData("offered")]
    [InlineData("withdrawn")]
    public async Task Reorder_EntryNotInWaitingStatus_Returns409(string targetStatus)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reorder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var first = await CreateAsync(client, org.AccessToken, location.Id, "Emma");
        await CreateAsync(client, org.AccessToken, location.Id, "Louis");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{first.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest(targetStatus)));

        var response = await ReorderRawAsync(client, org.AccessToken, first.Id, "down");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.waiting_list.not_reorderable_in_current_status", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reorder_EnrolledEntry_Returns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reorder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var first = await CreateAsync(client, org.AccessToken, location.Id, "Emma");
        await CreateAsync(client, org.AccessToken, location.Id, "Louis");
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{first.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("offered")));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{first.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("enrolled")));

        var response = await ReorderRawAsync(client, org.AccessToken, first.Id, "down");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.waiting_list.not_reorderable_in_current_status", await response.Content.ReadAsStringAsync());
    }
}
