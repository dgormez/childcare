using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 012a, User Story 5 — linking an entry to a child record: linking to an existing
/// child (FR-010), creating a new pre-filled child via feature 006's CreateChildCommand
/// (FR-011, research.md R5), leaving an enrolled entry unlinked until later (FR-012), and the
/// mutually-exclusive-input validation (neither or both of childId/createNewChild).
/// </summary>
public class ChildLinkTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<WaitingListEntryResponse> CreateEntryAsync(HttpClient client, string accessToken, Guid locationId) =>
        (await (await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", accessToken,
            new CreateWaitingListEntryRequest("Emma", "Peeters", new DateOnly(2025, 3, 10), "Sophie Peeters", null, null, locationId, null, null))))
            .Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Louis", "Janssens", new DateOnly(2022, 1, 15), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static Task<HttpResponseMessage> LinkRawAsync(HttpClient client, string accessToken, Guid entryId, Guid? childId, bool createNewChild) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entryId}/link-child", accessToken,
            new LinkChildToWaitingListEntryRequest(childId, createNewChild)));

    // ── T053: link to an existing child ─────────────────────────────────────────────────────

    [Fact]
    public async Task LinkExistingChild_SetsChildId()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await LinkRawAsync(client, org.AccessToken, entry.Id, child.Id, false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal(child.Id, updated.ChildId);
    }

    // ── T054: createNewChild pre-fills from the entry's name/DOB ────────────────────────────

    [Fact]
    public async Task CreateNewChild_CreatesChildPrefilledFromEntry_AndLinksIt()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id);

        var response = await LinkRawAsync(client, org.AccessToken, entry.Id, null, true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.NotNull(updated.ChildId);

        var childrenResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", org.AccessToken));
        var children = (await childrenResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        var createdChild = Assert.Single(children, c => c.Id == updated.ChildId);
        Assert.Equal(entry.ChildFirstName, createdChild.FirstName);
        Assert.Equal(entry.ChildLastName, createdChild.LastName);
        Assert.Equal(entry.DateOfBirth, createdChild.DateOfBirth);
    }

    // ── T055: an enrolled entry left unlinked remains linkable later ───────────────────────

    [Fact]
    public async Task EnrolledEntry_LeftUnlinked_IsStillLinkableLater()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("offered")));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/waiting-list/{entry.Id}/status", org.AccessToken,
            new TransitionWaitingListStatusRequest("enrolled")));
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await LinkRawAsync(client, org.AccessToken, entry.Id, child.Id, false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.Equal(child.Id, updated.ChildId);
        Assert.Equal("enrolled", updated.Status);
    }

    // ── T056: neither or both of childId/createNewChild -> 400 ─────────────────────────────

    [Fact]
    public async Task LinkChild_WithNeitherChildIdNorCreateNewChild_Returns400()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id);

        var response = await LinkRawAsync(client, org.AccessToken, entry.Id, null, false);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task LinkChild_WithBothChildIdAndCreateNewChild_Returns400()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await LinkRawAsync(client, org.AccessToken, entry.Id, child.Id, true);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task LinkChild_WithNonExistentChildId_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Link Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var entry = await CreateEntryAsync(client, org.AccessToken, location.Id);

        var response = await LinkRawAsync(client, org.AccessToken, entry.Id, Guid.NewGuid(), false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.children.not_found", await response.Content.ReadAsStringAsync());
    }
}
