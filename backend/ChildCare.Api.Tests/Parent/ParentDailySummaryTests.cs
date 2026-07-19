using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Parent;

/// <summary>
/// User Story 1 (SC-001, SC-003, SC-007): a parent sees an aggregated, visible_to_parent-
/// filtered daily summary for their own child, and is denied for a child they aren't a
/// contact of. Mirrors DailySummaryTests' seeding pattern (feature 009).
/// </summary>
public class ParentDailySummaryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static HttpRequestMessage ParentRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    // ── T042: aggregates visible events (naps/bottles/diapers/mood/temperature/medication/activities) ──

    [Fact]
    public async Task DailySummary_AggregatesVisibleEvents_Correctly()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentSummary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = DateTime.UtcNow.Date.AddHours(10);
        await PostChildEventAsync(client, deviceToken, child.Id, "feeding_bottle", today, new { ml = 120 });
        await PostChildEventAsync(client, deviceToken, child.Id, "diaper", today.AddMinutes(30), new { type = "wet" });
        await PostChildEventAsync(client, deviceToken, child.Id, "activity", today.AddHours(1), new { description = "Garden play" });

        var response = await client.SendAsync(ParentRequest(
            HttpMethod.Get, $"/api/parent/children/{child.Id}/daily-summary?date={DateOnly.FromDateTime(today):yyyy-MM-dd}", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;
        Assert.Equal(1, summary.BottlesCount);
        Assert.Equal(1, summary.DiaperChangesCount);
        Assert.Contains("Garden play", summary.Activities);
    }

    // ── T043: visible_to_parent=false events never appear, for any event type ──────

    [Fact]
    public async Task DailySummary_ExcludesInternalOnlyEvents_Always()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentSummaryHide Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var today = DateTime.UtcNow.Date.AddHours(10);
        await PostChildEventAsync(client, deviceToken, child.Id, "note", today, new { text = "Internal-only staff note" }, visibleToParent: false);
        await PostChildEventAsync(client, deviceToken, child.Id, "activity", today.AddMinutes(10), new { description = "Internal-only activity" }, visibleToParent: false);

        var response = await client.SendAsync(ParentRequest(
            HttpMethod.Get, $"/api/parent/children/{child.Id}/daily-summary?date={DateOnly.FromDateTime(today):yyyy-MM-dd}", parentToken));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal-only", body);
    }

    // ── T044: a parent cannot fetch a summary for a child they aren't a contact of ──

    [Fact]
    public async Task DailySummary_NotAContact_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentSummaryDenied Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");

        var response = await client.SendAsync(ParentRequest(
            HttpMethod.Get, $"/api/parent/children/{otherChild.Id}/daily-summary", parentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.parent.not_a_contact", await response.Content.ReadAsStringAsync());
    }

    // ── T044a: tenant isolation — cross-tenant child id never authorized ────────────

    [Fact]
    public async Task DailySummary_CrossTenantChildId_Returns403NotLeak()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"ParentSummaryTenantA {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"ParentSummaryTenantB {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var (childB, _, _) = await InviteAndLoginParentAsync(client, factory, orgB.Organisation.Slug, orgB.AccessToken);
        var (_, _, parentTokenA) = await InviteAndLoginParentAsync(client, factory, orgA.Organisation.Slug, orgA.AccessToken);

        var response = await client.SendAsync(ParentRequest(
            HttpMethod.Get, $"/api/parent/children/{childB.Id}/daily-summary", parentTokenA));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Feature 030 (US5, research.md R8) — a deactivated child's daily summary must remain
    // reachable read-only for the parent (no new authorization gap introduced by 030).
    [Fact]
    public async Task DailySummary_DeactivatedChild_StillSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentSummaryDeactivated Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/daily-summary", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── empty summary → clean zeroed response, not an error ─────────────────────────

    [Fact]
    public async Task DailySummary_NoEventsToday_ReturnsZeroedSummary()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ParentSummaryEmpty Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(ParentRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/daily-summary", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<DailySummaryResponse>())!;
        Assert.Equal(0, summary.NapsCount);
        Assert.Empty(summary.Activities);
    }
}
