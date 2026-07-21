using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 023, User Story 2 — duplicate flagging and origin tagging (FR-011). The duplicate
/// detection itself is 012a's existing, source-agnostic `WaitingListQueries.BuildFilteredList`
/// logic (a self-join on child name/DOB per location) — this suite confirms it already extends
/// correctly to a mix of self-registered and director-entered entries, since neither the
/// self-registration path nor the response mapper changed that behavior.
/// </summary>
public class WaitingListDuplicateFlagTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static SubmitPublicEnrollmentRequest SelfRegisteredRequest(string firstName, string lastName, DateOnly dob) => new(
        firstName, lastName, dob, new DateOnly(2026, 9, 1), "Sophie Peeters",
        $"sophie_{Guid.NewGuid():N}@example.com", null, null, "nl", Website: "");

    private static CreateWaitingListEntryRequest DirectorEnteredRequest(Guid locationId, string firstName, string lastName, DateOnly dob) =>
        new(firstName, lastName, dob, "Director Entered Contact", null, null, locationId, null, null);

    // ── T032: self-registered entry flagged as a duplicate of an existing entry ────────

    [Fact]
    public async Task SelfRegisteredEntry_MatchingExistingEntry_IsFlaggedDuplicate_BothVisible()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Duplicate Flag Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Marigold");
        var putResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var dob = new DateOnly(2025, 3, 10);
        var directorEntered = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/waiting-list", org.AccessToken,
            DirectorEnteredRequest(location.Id, "Liam", "Vandenberghe", dob)));
        Assert.Equal(HttpStatusCode.Created, directorEntered.StatusCode);
        var directorEntry = (await directorEntered.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;

        var selfRegistered = await client.PostAsJsonAsync(
            $"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}",
            SelfRegisteredRequest("Liam", "Vandenberghe", dob));
        Assert.Equal(HttpStatusCode.OK, selfRegistered.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}", org.AccessToken));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;

        // T033: both entries remain independently visible/actionable — neither auto-rejected.
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.True(e.IsDuplicate));

        var selfRegisteredEntry = Assert.Single(entries, e => e.Id != directorEntry.Id);
        Assert.Equal("selfRegistered", selfRegisteredEntry.Source);
        Assert.Equal("directorEntered", entries.Single(e => e.Id == directorEntry.Id).Source);
    }

    [Fact]
    public async Task SelfRegisteredEntry_NoMatch_IsNotFlaggedDuplicate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"No Duplicate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Foxglove");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));

        var response = await client.PostAsJsonAsync(
            $"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}",
            SelfRegisteredRequest("Nora", "Peeters", new DateOnly(2025, 6, 1)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}", org.AccessToken));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;

        Assert.False(Assert.Single(entries).IsDuplicate);
    }
}
