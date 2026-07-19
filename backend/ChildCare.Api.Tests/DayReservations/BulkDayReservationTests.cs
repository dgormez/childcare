using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.DayReservations;

/// <summary>
/// Feature 030 User Story 1 — a parent with 2+ linked children can submit one bulk day
/// reservation that fans out into one independent per-child <c>DayReservation</c> row
/// (spec.md FR-001/FR-002/FR-003), reusing 013a/013f's existing per-child validation via
/// mediator dispatch (research.md R1) rather than a parallel rule set.
/// </summary>
public class BulkDayReservationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly FutureMonday = new(2027, 8, 2);

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Bulk Reservation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        return (client, org, location);
    }

    private static Task<HttpResponseMessage> SubmitBulkRawAsync(HttpClient client, string parentToken, BulkDayReservationRequest req) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/parent/day-reservations/bulk", parentToken, req));

    private static async Task<BulkDayReservationResponse> SubmitBulkAsync(HttpClient client, string parentToken, BulkDayReservationRequest req)
    {
        var response = await SubmitBulkRawAsync(client, parentToken, req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BulkDayReservationResponse>())!;
    }

    [Fact]
    public async Task Submit_TwoActiveChildrenSameLocation_CreatesOneReservationEach()
    {
        var (client, org, location) = await SetupAsync();
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);

        var result = await SubmitBulkAsync(client, parentToken,
            new BulkDayReservationRequest([child1.Id, child2.Id], "absence", FutureMonday, null, "Griep"));

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.True(r.Succeeded));
        Assert.All(result.Results, r => Assert.Equal("pending", r.Reservation!.Status));
        Assert.Equal([child1.Id, child2.Id], result.Results.Select(r => r.ChildId).OrderBy(id => id).ToList().OrderBy(id => id));
    }

    [Fact]
    public async Task Submit_OneChildLocationHasTypeDisabled_OtherStillSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Bulk Reservation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var allowedLocation = await CreateLocationAsync(client, org.AccessToken, "Allowed");
        var blockedLocation = await CreateLocationAsync(client, org.AccessToken, "Blocked");

        // Disable absence requests at blockedLocation only (013f).
        var disableResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{blockedLocation.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("disabled", "approval", "disabled", 0, false)));
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        var (allowedChild, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var blockedChild = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, blockedChild.Id, contact.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, allowedChild.Id, allowedLocation.Id, FutureMonday.DayOfWeek);
        await CreateAndActivateContractAsync(client, org.AccessToken, blockedChild.Id, blockedLocation.Id, FutureMonday.DayOfWeek);

        var result = await SubmitBulkAsync(client, parentToken,
            new BulkDayReservationRequest([allowedChild.Id, blockedChild.Id], "absence", FutureMonday, null, null));

        Assert.Equal(2, result.Results.Count);
        var allowedResult = result.Results.Single(r => r.ChildId == allowedChild.Id);
        var blockedResult = result.Results.Single(r => r.ChildId == blockedChild.Id);
        Assert.True(allowedResult.Succeeded);
        Assert.False(blockedResult.Succeeded);
        Assert.Equal("errors.day_reservations.request_type_disabled", blockedResult.ErrorKey);
    }

    [Fact]
    public async Task Submit_IncludingUnlinkedChild_ThatEntryFailsWithoutBlockingOthers()
    {
        var (client, org, location) = await SetupAsync();
        var (linkedChild, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var unlinkedChild = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);

        var result = await SubmitBulkAsync(client, parentToken,
            new BulkDayReservationRequest([linkedChild.Id, unlinkedChild.Id], "absence", FutureMonday, null, null));

        Assert.Equal(2, result.Results.Count);
        var linkedResult = result.Results.Single(r => r.ChildId == linkedChild.Id);
        var unlinkedResult = result.Results.Single(r => r.ChildId == unlinkedChild.Id);
        Assert.True(linkedResult.Succeeded);
        Assert.False(unlinkedResult.Succeeded);
        Assert.Equal("errors.day_reservations.child_not_linked", unlinkedResult.ErrorKey);
    }

    [Fact]
    public async Task Submit_SiblingsAtDifferentLocations_EachEvaluatesOwnLocationNoticeHours()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Bulk Reservation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var openLocation = await CreateLocationAsync(client, org.AccessToken, "Open");
        var strictLocation = await CreateLocationAsync(client, org.AccessToken, "Strict");

        // A date 3 days out (rather than the file's fixed FutureMonday constant, which is
        // eventually too far in the future for any notice window under the 8760h/1-year
        // validator ceiling — errors.location.reservation_settings.notice_hours_out_of_range)
        // plus a 240h (10 day) notice window guarantees a violation regardless of which
        // real-world date this test happens to run on.
        var nearFutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var strictResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{strictLocation.Id}/reservation-settings", org.AccessToken,
            new UpdateLocationReservationSettingsRequest("approval", "approval", "disabled", 240, false)));
        Assert.Equal(HttpStatusCode.OK, strictResponse.StatusCode);

        var (openChild, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var strictChild = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, strictChild.Id, contact.Id);
        await CreateAndActivateContractAsync(client, org.AccessToken, openChild.Id, openLocation.Id, nearFutureDate.DayOfWeek);
        await CreateAndActivateContractAsync(client, org.AccessToken, strictChild.Id, strictLocation.Id, nearFutureDate.DayOfWeek);

        var result = await SubmitBulkAsync(client, parentToken,
            new BulkDayReservationRequest([openChild.Id, strictChild.Id], "absence", nearFutureDate, null, null));

        var openResult = result.Results.Single(r => r.ChildId == openChild.Id);
        var strictResult = result.Results.Single(r => r.ChildId == strictChild.Id);
        Assert.True(openResult.Succeeded);
        Assert.False(strictResult.Succeeded);
        Assert.Equal("errors.day_reservations.notice_period_required", strictResult.ErrorKey);
    }

    [Fact]
    public async Task Submit_ForUnlinkedCallerEntirely_ChildNotLinkedForEveryEntry()
    {
        var (client, org, location) = await SetupAsync();
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild1 = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);
        var otherChild2 = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);

        var result = await SubmitBulkAsync(client, parentToken,
            new BulkDayReservationRequest([otherChild1.Id, otherChild2.Id], "absence", FutureMonday, null, null));

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.False(r.Succeeded));
        Assert.All(result.Results, r => Assert.Equal("errors.day_reservations.child_not_linked", r.ErrorKey));
    }
}
