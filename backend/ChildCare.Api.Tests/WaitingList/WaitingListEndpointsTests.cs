using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 012a, User Story 1 — director creates and reviews the waiting list: creation with
/// full/minimal fields, priority-ordered listing, the default `waiting`-only filter vs.
/// `all`/explicit-status filters (FR-003), duplicate flagging (FR-004), and DirectorOnly
/// enforcement across every endpoint (FR-016/FR-017, T062).
/// </summary>
public class WaitingListEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateWaitingListEntryRequest MinimalRequest(Guid locationId, string firstName = "Emma", string lastName = "Peeters") =>
        new(firstName, lastName, new DateOnly(2025, 3, 10), "Sophie Peeters", null, null, locationId, null, null);

    private static CreateWaitingListEntryRequest FullRequest(Guid locationId) => new(
        "Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters",
        "sophie@example.com", "+32 9 123 45 67", locationId, new DateOnly(2026, 9, 1), "Prefers mornings");

    private static Task<HttpResponseMessage> CreateRawAsync(HttpClient client, string accessToken, CreateWaitingListEntryRequest req) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", accessToken, req));

    private static async Task<WaitingListEntryResponse> CreateAsync(HttpClient client, string accessToken, CreateWaitingListEntryRequest req)
    {
        var response = await CreateRawAsync(client, accessToken, req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
    }

    private static Task<HttpResponseMessage> ListRawAsync(HttpClient client, string accessToken, Guid locationId, string? status = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get,
            status is null ? $"/api/waiting-list?locationId={locationId}" : $"/api/waiting-list?locationId={locationId}&status={status}",
            accessToken));

    private static async Task<List<WaitingListEntryResponse>> ListAsync(HttpClient client, string accessToken, Guid locationId, string? status = null)
    {
        var response = await ListRawAsync(client, accessToken, locationId, status);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;
    }

    private async Task InsertUserWithRoleAsync(Guid tenantId, string email, string password, UserRole role)
    {
        var schema = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schema);
        db.Users.Add(new TenantUser
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Name = $"Test {role}",
            Role = role,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<string> LoginAsync(HttpClient client, string slug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return body.AccessToken;
    }

    // ── T014: create then list, default status=waiting ─────────────────────────────────────

    [Fact]
    public async Task Create_ThenList_ReturnsEntryFilteredByLocationAndDefaultStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var entry = await CreateAsync(client, org.AccessToken, FullRequest(location.Id));

        var entries = await ListAsync(client, org.AccessToken, location.Id);
        Assert.Contains(entries, e => e.Id == entry.Id);
        Assert.All(entries, e => Assert.Equal("waiting", e.Status));
    }

    // ── T015: minimal required fields only ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithOnlyRequiredFields_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var entry = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id));

        Assert.Null(entry.ContactEmail);
        Assert.Null(entry.ContactPhone);
        Assert.Null(entry.RequestedStartDate);
        Assert.Null(entry.Notes);
    }

    // ── T016: duplicate flag, including across the default waiting-only filter ─────────────

    [Fact]
    public async Task Create_TwoEntriesSameChildNameAndDob_BothFlaggedDuplicate_EvenAcrossStatusFilter()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var first = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id));
        var second = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id));

        // Move the first to `withdrawn` so it drops out of the default waiting-only view, but
        // the duplicate flag on the still-`waiting` second entry must still be true (FR-004).
        var withdraw = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{first.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("withdrawn")));
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        var waitingOnly = await ListAsync(client, org.AccessToken, location.Id);
        var secondInWaitingView = Assert.Single(waitingOnly, e => e.Id == second.Id);
        Assert.True(secondInWaitingView.IsDuplicate);
        Assert.DoesNotContain(waitingOnly, e => e.Id == first.Id);

        var allView = await ListAsync(client, org.AccessToken, location.Id, "all");
        var firstInAllView = Assert.Single(allView, e => e.Id == first.Id);
        Assert.True(firstInAllView.IsDuplicate);
    }

    [Fact]
    public async Task Create_UniqueEntries_NotFlaggedDuplicate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var entry = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id));

        Assert.False(entry.IsDuplicate);
    }

    // ── T017: status=all returns every status ───────────────────────────────────────────────

    [Fact]
    public async Task List_StatusAll_ReturnsEntriesAcrossEveryStatus()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var waiting = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id, "Emma", "Peeters"));
        var toOffer = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id, "Louis", "Janssens"));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{toOffer.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("offered")));

        var all = await ListAsync(client, org.AccessToken, location.Id, "all");

        Assert.Contains(all, e => e.Id == waiting.Id && e.Status == "waiting");
        Assert.Contains(all, e => e.Id == toOffer.Id && e.Status == "offered");
    }

    // ── T018/T062: DirectorOnly across every endpoint ────────────────────────────────────────

    [Fact]
    public async Task Create_AsStaffOrParentToken_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(org.Organisation.Id, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(org.Organisation.Id, parentEmail, "password123", UserRole.Parent);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        var staffCreate = await CreateRawAsync(client, staffToken, MinimalRequest(location.Id));
        var parentCreate = await CreateRawAsync(client, parentToken, MinimalRequest(location.Id));

        Assert.Equal(HttpStatusCode.Forbidden, staffCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parentCreate.StatusCode);
    }

    [Fact]
    public async Task EveryEndpoint_AsStaffToken_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id));
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(org.Organisation.Id, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var list = await ListRawAsync(client, staffToken, location.Id);
        var update = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/waiting-list/{entry.Id}", staffToken,
            new UpdateWaitingListEntryRequest("Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters", null, null, location.Id, null, null)));
        var reorder = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/reorder", staffToken,
            new ReorderWaitingListEntryRequest("up")));
        var status = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/status", staffToken,
            new TransitionWaitingListStatusRequest("offered")));
        var occupancy = await client.SendAsync(AuthedRequest(HttpMethod.Get,
            $"/api/waiting-list/occupancy?locationId={location.Id}&from=2026-09-01&to=2026-09-07", staffToken));
        var link = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/link-child", staffToken,
            new LinkChildToWaitingListEntryRequest(null, true)));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reorder.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, occupancy.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, link.StatusCode);
    }

    // ── Update (non-lifecycle fields) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_NonLifecycleFields_Persist()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateAsync(client, org.AccessToken, MinimalRequest(location.Id));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Patch, $"/api/waiting-list/{entry.Id}", org.AccessToken,
            new UpdateWaitingListEntryRequest("Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters",
                "sophie@example.com", "+32 9 000 00 00", location.Id, new DateOnly(2026, 9, 1), "Updated notes")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal("sophie@example.com", updated.ContactEmail);
        Assert.Equal("Updated notes", updated.Notes);
        Assert.Equal("waiting", updated.Status);
    }

    // ── Convergence T065: Notes beyond the 2000-char column limit is a clean 400, not a ────
    // raw DB error.

    [Fact]
    public async Task Create_WithOverlongNotes_Returns422Validation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Waiting List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var overlongNotes = new string('a', 2001);

        var response = await CreateRawAsync(client, org.AccessToken, new CreateWaitingListEntryRequest(
            "Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters", null, null, location.Id, null, overlongNotes));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
