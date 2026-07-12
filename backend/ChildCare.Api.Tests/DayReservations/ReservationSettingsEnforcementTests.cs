using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.DayReservations;

/// <summary>
/// Feature 013f User Stories 2 &amp; 3 — the parent-facing enforcement half of reservation
/// settings: disabled types are rejected server-side, informational-mode requests auto-approve
/// with the same downstream effects an approval would have, and the notice-hours window plus
/// multi-location most-restrictive-wins resolution (FR-017) are enforced regardless of client
/// behavior.
/// </summary>
public class ReservationSettingsEnforcementTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 7, 13);
    private static readonly DateOnly Tuesday = new(2026, 7, 14);

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reservation Enforcement Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        return (client, org, location);
    }

    private static async Task<LocationResponse> SetReservationSettingsAsync(
        HttpClient client, string directorToken, Guid locationId,
        string absencesMode = "approval", string extrasMode = "approval", string swapsMode = "disabled", int noticeHours = 0)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{locationId}/reservation-settings", directorToken,
            new UpdateLocationReservationSettingsRequest(absencesMode, extrasMode, swapsMode, noticeHours, false)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
    }

    private static Task<HttpResponseMessage> SubmitRawAsync(HttpClient client, string parentToken, SubmitDayReservationRequest req) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/day-reservations", parentToken, req));

    private static async Task<List<DayReservationResponse>> ListAsync(HttpClient client, string directorToken, string status)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/day-reservations?status={status}", directorToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<DayReservationResponse>>())!;
    }

    private static async Task<int> AttendanceCountAsync(HttpClient client, string directorToken, Guid locationId, Guid childId, DateOnly date)
    {
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/attendance/?locationId={locationId}&date={date:yyyy-MM-dd}", directorToken));
        var attendance = (await response.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        return attendance.Items.Count(a => a.ChildId == childId);
    }

    // ── US2: disabled type rejected server-side (FR-007) ────────────────────────────────────

    [Fact]
    public async Task Submit_SwapType_DisabledAtLocation_Returns403AndCreatesNoRow()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        // reservation_swaps_mode defaults to "disabled" — no explicit settings call needed.

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "exchange", Tuesday, Monday, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.day_reservations.request_type_disabled", await response.Content.ReadAsStringAsync());
        Assert.Empty(await ListAsync(client, org.AccessToken, "all"));
    }

    // ── US2: informational auto-approval, same downstream effect as approval (FR-008/FR-009) ─

    [Fact]
    public async Task Submit_Absence_InformationalMode_AutoApprovesWithAttendanceRecordAndNullDecidedBy()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, absencesMode: "informational");

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, "Koorts"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("approved", reservation.Status);
        Assert.Null(reservation.DecidedBy);
        Assert.NotNull(reservation.DecidedAt);
        Assert.True(reservation.AbsenceJustified);

        Assert.Equal(1, await AttendanceCountAsync(client, org.AccessToken, location.Id, child.Id, Monday));
    }

    [Fact]
    public async Task Submit_Absence_InformationalMode_ClosureDayConflict_RejectedNotAutoApproved()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, absencesMode: "informational");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/closures", org.AccessToken,
            new CreateClosureDayRequest(location.Id, Monday, "Sluitingsdag", "holiday", true)));
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closure.Id}/publish", org.AccessToken, new PublishClosureDayRequest(false)));

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ListAsync(client, org.AccessToken, "all"));
    }

    [Fact]
    public async Task Submit_Extra_InformationalMode_AutoApproves_NoAttendanceRecordCreated()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, extrasMode: "informational");

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("approved", reservation.Status);
        Assert.Null(reservation.DecidedBy);
        Assert.Equal(0, await AttendanceCountAsync(client, org.AccessToken, location.Id, child.Id, Tuesday));
    }

    [Fact]
    public async Task ListApproved_IncludesSystemAutoApprovedRowAlongsideDirectorDecided()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, absencesMode: "informational");

        var autoApproved = (await (await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null)))
            .Content.ReadFromJsonAsync<DayReservationResponse>())!;

        var approvedList = await ListAsync(client, org.AccessToken, "approved");

        var found = Assert.Single(approvedList, r => r.Id == autoApproved.Id);
        Assert.Null(found.DecidedBy);
    }

    [Fact]
    public async Task Submit_Absence_ApprovalMode_UnchangedFrom013a()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        // Default is already "approval" — no settings call needed, this is the regression guard.

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("pending", reservation.Status);
    }

    // ── US3: notice-hours window (FR-012) ────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_InsideNoticeHoursWindow_Returns400AndCreatesNoRow()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        // A huge notice window guarantees "Monday" (a fixed near-term future date) is inside
        // it, regardless of which real-world date this test happens to run on.
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, noticeHours: 8760);

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.day_reservations.notice_period_required", await response.Content.ReadAsStringAsync());
        Assert.Empty(await ListAsync(client, org.AccessToken, "all"));
    }

    [Fact]
    public async Task Submit_NoticeHoursZero_StillSucceeds()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        // noticeHours defaults to 0 — no settings call needed. Regardless of how close
        // "Monday" is to the real-world moment this test runs, 0 means no restriction.

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "absence", Monday, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── US3: split-location most-restrictive-wins (FR-017) ──────────────────────────────────

    [Fact]
    public async Task SplitLocationChild_HigherNoticeHoursGoverns()
    {
        var (client, org, locationA) = await SetupAsync();
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Second");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        // Split-location contract: Monday at Location A (0h notice), Tuesday at Location B
        // (a huge notice window) — but "extra" has no weekday match, so both locations are
        // candidates regardless of which weekday each is contracted for (research.md R3).
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, locationA.Id, Monday.DayOfWeek);
        // ContractSplitLocationTests precedent: a child can hold two simultaneous active
        // contracts at different locations provided contracted weekdays don't overlap.
        var secondContractResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken,
            new CreateContractRequest(locationB.Id, new DateOnly(2020, 1, 1), null, Days(Tuesday.DayOfWeek), 3500, null)));
        var secondContract = (await secondContractResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var secondActivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{secondContract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, secondActivateResponse.StatusCode);

        await SetReservationSettingsAsync(client, org.AccessToken, locationA.Id, noticeHours: 0);
        await SetReservationSettingsAsync(client, org.AccessToken, locationB.Id, noticeHours: 8760);

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Monday, null, null));

        // Location B's much stricter notice window governs even though A alone would allow it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.day_reservations.notice_period_required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SplitLocationChild_OneLocationDisables_RequestRejected()
    {
        var (client, org, locationA) = await SetupAsync();
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Second");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, locationA.Id, Monday.DayOfWeek);
        var secondContractResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken,
            new CreateContractRequest(locationB.Id, new DateOnly(2020, 1, 1), null, Days(Tuesday.DayOfWeek), 3500, null)));
        var secondContract = (await secondContractResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{secondContract.Id}/activate", org.AccessToken));

        await SetReservationSettingsAsync(client, org.AccessToken, locationA.Id, extrasMode: "approval");
        await SetReservationSettingsAsync(client, org.AccessToken, locationB.Id, extrasMode: "disabled");

        // "extra" has no weekday match, so both locations are candidates — B's "disabled" wins.
        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChildWithNoActiveContract_EnforcementSkipped_ProceedsAsApproval()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        // No contract at all for this child (e.g. still on the waiting list, 012a).
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, extrasMode: "disabled", noticeHours: 999);

        var response = await SubmitRawAsync(client, parentToken, new SubmitDayReservationRequest(child.Id, "extra", Tuesday, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = (await response.Content.ReadFromJsonAsync<DayReservationResponse>())!;
        Assert.Equal("pending", reservation.Status);
    }

    // ── Polish: parent-facing reservation-availability read ─────────────────────────────────

    [Fact]
    public async Task ReservationAvailability_ReturnsResolvedModePerType()
    {
        var (client, org, location) = await SetupAsync();
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, Monday.DayOfWeek);
        await SetReservationSettingsAsync(client, org.AccessToken, location.Id, absencesMode: "informational", extrasMode: "approval", swapsMode: "disabled", noticeHours: 12);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/reservation-availability", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = (await response.Content.ReadFromJsonAsync<ReservationAvailabilityResponse>())!;
        Assert.Equal("informational", availability.Absence);
        Assert.Equal("approval", availability.Extra);
        Assert.Equal("disabled", availability.Exchange);
        Assert.Equal(12, availability.NoticeHours);
    }

    [Fact]
    public async Task ReservationAvailability_SplitLocationChild_ReturnsMostRestrictivePerType()
    {
        var (client, org, locationA) = await SetupAsync();
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Second");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, locationA.Id, Monday.DayOfWeek);
        var secondContractResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken,
            new CreateContractRequest(locationB.Id, new DateOnly(2020, 1, 1), null, Days(Tuesday.DayOfWeek), 3500, null)));
        var secondContract = (await secondContractResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{secondContract.Id}/activate", org.AccessToken));
        await SetReservationSettingsAsync(client, org.AccessToken, locationA.Id, extrasMode: "approval");
        await SetReservationSettingsAsync(client, org.AccessToken, locationB.Id, extrasMode: "disabled");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/reservation-availability", parentToken));

        var availability = (await response.Content.ReadFromJsonAsync<ReservationAvailabilityResponse>())!;
        Assert.Equal("disabled", availability.Extra);
    }

    [Fact]
    public async Task ReservationAvailability_ChildNotLinked_Returns403()
    {
        var (client, org, _) = await SetupAsync();
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{otherChild.Id}/reservation-availability", parentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
