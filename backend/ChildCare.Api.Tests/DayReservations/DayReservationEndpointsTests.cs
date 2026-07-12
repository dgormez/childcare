using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.DayReservations;

/// <summary>
/// Feature 013a — day reservations (absence/extra/exchange requests + director approval queue).
/// Covers spec.md's five user stories: absence submit/approve/reject with attendance + push
/// side effects (US1), the mixed-type/empty-state queue (US2), extra-day requests with no
/// attendance side effect (US3), exchange-day requests with contracted-day/closure-day
/// validation (US4), and cancellation + own-request history (US5).
/// </summary>
public class DayReservationEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 7, 13);
    private static readonly DateOnly Tuesday = new(2026, 7, 14);

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Day Reservation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        return (client, org, location);
    }

    private static Task<HttpResponseMessage> SubmitRawAsync(HttpClient client, string parentToken, SubmitDayReservationRequest req) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/day-reservations", parentToken, req));

    private static async Task<DayReservationResponse> SubmitAsync(HttpClient client, string parentToken, SubmitDayReservationRequest req)
    {
        var response = await SubmitRawAsync(client, parentToken, req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
    }

    private static Task<HttpResponseMessage> ApproveRawAsync(HttpClient client, string directorToken, Guid id, bool? justified = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/day-reservations/{id}/approve", directorToken, new ApproveDayReservationRequest(justified)));

    private static Task<HttpResponseMessage> RejectRawAsync(HttpClient client, string directorToken, Guid id, string? notes = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/day-reservations/{id}/reject", directorToken, new RejectDayReservationRequest(notes)));

    private static Task<HttpResponseMessage> CancelRawAsync(HttpClient client, string parentToken, Guid id) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/day-reservations/{id}/cancel", parentToken));

    private static async Task<List<DayReservationResponse>> ListPendingAsync(HttpClient client, string directorToken, string? status = null)
    {
        var url = status is null ? "/api/day-reservations" : $"/api/day-reservations?status={status}";
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, url, directorToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<DayReservationResponse>>())!;
    }

    private static async Task<List<DayReservationResponse>> ListMineAsync(HttpClient client, string parentToken)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/day-reservations/mine", parentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<DayReservationResponse>>())!;
    }

    private static async Task<ClosureDayResponse> CreateAndPublishClosureAsync(HttpClient client, string accessToken, Guid locationId, DateOnly date)
    {
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/closures", accessToken,
            new CreateClosureDayRequest(locationId, date, "Sluitingsdag", "holiday", true)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        var publishResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closure.Id}/publish", accessToken, new PublishClosureDayRequest(false)));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        return closure;
    }

    private static async Task<string> LoginAsync(HttpClient client, string slug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return body.AccessToken;
    }

    // Feature 013f: reservation_swaps_mode defaults to "disabled" (unlike absence/extra, which
    // default to "approval") — every pre-existing exchange test in this file predates that
    // setting and needs to explicitly opt in to exercise 013a's original exchange behavior.
    private static async Task AllowExchangeRequestsAsync(HttpClient client, string directorToken, Guid locationId)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{locationId}/reservation-settings", directorToken,
            new UpdateLocationReservationSettingsRequest("approval", "approval", "approval", 0, false)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── US1: absence submit ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_Absence_AsLinkedParent_PersistsAsPending()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);

        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, "Koorts"));

        Assert.Equal("pending", reservation.Status);
        Assert.Equal("absence", reservation.Type);
        Assert.Equal("Koorts", reservation.Reason);
    }

    [Fact]
    public async Task Submit_Absence_MoreThanOneDayInThePast_Returns422Validation()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var farPast = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", farPast, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Submit_ForUnlinkedChild_Returns403()
    {
        var (client, org, location) = await SetupAsync();
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(otherChild.Id, "absence", Tuesday, null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ForUnlinkedOrNotOwnRequest_Returns403()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, firstParentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var secondParentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InviteAndLoginSecondParentForChildAsync(client, factory, org.Organisation.Slug, org.AccessToken, child.Id, secondParentEmail);
        var secondParentToken = await LoginAsync(client, org.Organisation.Slug, secondParentEmail, "password123");
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var reservation = await SubmitAsync(client, firstParentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        // Second parent is linked to the same child but did not submit this request (FR-014).
        var response = await CancelRawAsync(client, secondParentToken, reservation.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── US1: approve/reject ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_Absence_WithJustifiedTrue_CreatesAttendanceRecordAndNotifiesParent()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[test]")));
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, "Koorts"));

        var response = await ApproveRawAsync(client, org.AccessToken, reservation.Id, justified: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("approved", approved.Status);
        Assert.True(approved.AbsenceJustified);

        var attendanceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/attendance/?locationId={location.Id}&date={Monday:yyyy-MM-dd}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, attendanceResponse.StatusCode);
        var attendance = (await attendanceResponse.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        var record = Assert.Single(attendance.Items, a => a.ChildId == child.Id);
        Assert.Equal("absent", record.Status);
        Assert.True(record.AbsenceJustified);

        var push = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Contains(push.Sent, p => p.PushToken == "ExponentPushToken[test]");
    }

    [Fact]
    public async Task Reject_WithNote_NoAttendanceRecordCreated_NotificationIncludesNote()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/parent/push-token", parentToken, new RegisterPushTokenRequest("ExponentPushToken[test]")));
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        var response = await RejectRawAsync(client, org.AccessToken, reservation.Id, "Te laat gemeld");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("Te laat gemeld", rejected.DirectorNotes);

        var attendanceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/attendance/?locationId={location.Id}&date={Monday:yyyy-MM-dd}", org.AccessToken));
        var attendance = (await attendanceResponse.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        Assert.DoesNotContain(attendance.Items, a => a.ChildId == child.Id);

        var push = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Contains(push.Sent, p => p.PushToken == "ExponentPushToken[test]" && p.Body.Contains("Te laat gemeld"));
    }

    [Fact]
    public async Task Approve_Absence_DateBecameClosureDay_FailsCleanly()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        // Closure day published AFTER the request was submitted (FR-011's genuine race).
        await CreateAndPublishClosureAsync(client, org.AccessToken, location.Id, Monday);

        var response = await ApproveRawAsync(client, org.AccessToken, reservation.Id, justified: true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stillPending = Assert.Single(await ListPendingAsync(client, org.AccessToken), r => r.Id == reservation.Id);
        Assert.Equal("pending", stillPending.Status);
    }

    [Fact]
    public async Task ConcurrentApproveAndReject_OnlyOneSucceeds()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        var approveTask = ApproveRawAsync(client, org.AccessToken, reservation.Id, justified: true);
        var rejectTask = RejectRawAsync(client, org.AccessToken, reservation.Id, "race");
        var responses = await Task.WhenAll(approveTask, rejectTask);

        var statuses = responses.Select(r => r.StatusCode).ToArray();
        Assert.Single(statuses, s => s is HttpStatusCode.OK);
        Assert.Single(statuses, s => s is HttpStatusCode.Conflict);
    }

    // ── Authorization boundary ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParentEndpoints_AsDirectorToken_Returns403_AndDirectorEndpoints_AsParentToken_Returns403()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        var directorSubmits = await SubmitRawAsync(client, org.AccessToken, new SubmitDayReservationRequest(child.Id, "absence", Tuesday, null, null));
        var parentLists = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/day-reservations", parentToken));
        var parentApproves = await ApproveRawAsync(client, parentToken, reservation.Id, justified: true);

        Assert.Equal(HttpStatusCode.Forbidden, directorSubmits.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parentLists.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parentApproves.StatusCode);
    }

    // ── US2: mixed-type/empty-state queue ────────────────────────────────────────────────────

    [Fact]
    public async Task ListPending_ReturnsMixedTypesNewestFirst()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);

        var first = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));
        var second = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));

        var pending = await ListPendingAsync(client, org.AccessToken);

        var firstIndex = pending.FindIndex(r => r.Id == first.Id);
        var secondIndex = pending.FindIndex(r => r.Id == second.Id);
        Assert.True(secondIndex < firstIndex, "newest (second submitted) should sort first");
    }

    [Fact]
    public async Task ListPending_NoRequests_ReturnsEmptyArray()
    {
        var (client, org, location) = await SetupAsync();

        var pending = await ListPendingAsync(client, org.AccessToken);

        Assert.Empty(pending);
    }

    // ── US3: extra day ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_Extra_PersistsAsPending()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, "Extra opvang nodig"));

        Assert.Equal("pending", reservation.Status);
        Assert.Equal("extra", reservation.Type);
    }

    [Fact]
    public async Task Approve_Extra_TransitionsApproved_NoAttendanceRecordCreated()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));

        var response = await ApproveRawAsync(client, org.AccessToken, reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("approved", approved.Status);

        var attendanceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/attendance/?locationId={location.Id}&date={Tuesday:yyyy-MM-dd}", org.AccessToken));
        var attendance = (await attendanceResponse.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        Assert.DoesNotContain(attendance.Items, a => a.ChildId == child.Id);
    }

    // ── US4: exchange day ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_Exchange_ValidContractedDay_PersistsAsPending()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await AllowExchangeRequestsAsync(client, org.AccessToken, location.Id);

        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "exchange", Tuesday, Monday, "Ruildag"));

        Assert.Equal("pending", reservation.Status);
        Assert.Equal("exchange", reservation.Type);
        Assert.Equal(Monday, reservation.ExchangeForDate);
    }

    [Fact]
    public async Task Submit_Exchange_NotAContractedDay_Returns400()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        var notContractedDay = new DateOnly(2026, 7, 15); // Wednesday — never contracted.

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "exchange", Tuesday, notContractedDay, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_Exchange_ClosureDayTarget_Returns400()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await CreateAndPublishClosureAsync(client, org.AccessToken, location.Id, Tuesday);

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "exchange", Tuesday, Monday, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approve_Exchange_TransitionsApproved_NoAttendanceRecordCreated()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await AllowExchangeRequestsAsync(client, org.AccessToken, location.Id);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "exchange", Tuesday, Monday, null));

        var response = await ApproveRawAsync(client, org.AccessToken, reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attendanceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/attendance/?locationId={location.Id}&date={Tuesday:yyyy-MM-dd}", org.AccessToken));
        var attendance = (await attendanceResponse.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        Assert.DoesNotContain(attendance.Items, a => a.ChildId == child.Id);
    }

    // ── US5: cancel + own-request history ────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_OwnPendingRequest_RemovesFromActiveQueue()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));

        var response = await CancelRawAsync(client, parentToken, reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("cancelled", cancelled.Status);
        Assert.DoesNotContain(await ListPendingAsync(client, org.AccessToken), r => r.Id == reservation.Id);
    }

    [Fact]
    public async Task Cancel_AlreadyDecidedRequest_Returns409()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var reservation = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));
        await ApproveRawAsync(client, org.AccessToken, reservation.Id);

        var response = await CancelRawAsync(client, parentToken, reservation.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Mine_ReturnsAllStatusesAcrossLinkedChildren_NewestFirst()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var pending = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));
        var toReject = await SubmitAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Monday, null, null));
        await RejectRawAsync(client, org.AccessToken, toReject.Id, null);

        var mine = await ListMineAsync(client, parentToken);

        Assert.Contains(mine, r => r.Id == pending.Id && r.Status == "pending");
        Assert.Contains(mine, r => r.Id == toReject.Id && r.Status == "rejected");
        Assert.True(mine.FindIndex(r => r.Id == toReject.Id) < mine.FindIndex(r => r.Id == pending.Id));
    }
}
