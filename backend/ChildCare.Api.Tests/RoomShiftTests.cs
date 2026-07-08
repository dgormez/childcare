using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>User Story 3 (feature 008a): the core select-then-PIN shift-register loop — roster,
/// check-in/out, lockout, director corrections, deactivation, and multi-location eligibility
/// (T033-T044). Also covers User Story 5's administrator-confirmation reuse of the same PIN/
/// lockout path (T061-T064).</summary>
public class RoomShiftTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T033: roster returns every eligible caregiver as a card, including one with no photo ──

    [Fact]
    public async Task GetRoster_ReturnsEveryEligibleCaregiver_IncludingOneWithNoPhoto_PhotoUrlNullNeverOmitted()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Roster Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await GetRosterAsync(client, deviceToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"photoUrl\":null", body);

        var cards = (await response.Content.ReadFromJsonAsync<List<RoomRosterCardResponse>>())!;
        var card = Assert.Single(cards, c => c.StaffProfileId == staff.Id);
        Assert.Equal("Jane", card.FirstName);
        Assert.Null(card.PhotoUrl);
        Assert.False(card.CheckedIn);
    }

    // ── T034: check-in for a not-yet-checked-in caregiver, roster reflects checkedIn:true after ──

    [Fact]
    public async Task CheckIn_NotYetCheckedIn_RecordsCheckIn_RosterReflectsImmediately()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var checkInResponse = await CheckInAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);

        var roster = (await (await GetRosterAsync(client, deviceToken)).Content.ReadFromJsonAsync<List<RoomRosterCardResponse>>())!;
        var card = Assert.Single(roster, c => c.StaffProfileId == staff.Id);
        Assert.True(card.CheckedIn);
        Assert.NotNull(card.CheckedInAt);
    }

    // ── T035: check-out for a checked-in caregiver removes them from the checked-in set ──

    [Fact]
    public async Task CheckOut_CheckedIn_RecordsCheckOut_RemovesFromCheckedInSet()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckOut Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, staff.Id, "1234")).StatusCode);
        var checkOutResponse = await CheckOutAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.OK, checkOutResponse.StatusCode);

        var roster = (await (await GetRosterAsync(client, deviceToken)).Content.ReadFromJsonAsync<List<RoomRosterCardResponse>>())!;
        var card = Assert.Single(roster, c => c.StaffProfileId == staff.Id);
        Assert.False(card.CheckedIn);
    }

    // ── T036: caregiver A and caregiver B can be checked in simultaneously, neither blocks the other ──

    [Fact]
    public async Task CheckIn_TwoCaregivers_BothCheckedInSimultaneously_NeitherBlocksTheOther()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Simultaneous Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staffA = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111", "Alice");
        var staffB = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2222", "Bob");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, staffA.Id, "1111")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, staffB.Id, "2222")).StatusCode);

        var roster = (await (await GetRosterAsync(client, deviceToken)).Content.ReadFromJsonAsync<List<RoomRosterCardResponse>>())!;
        Assert.True(roster.Single(c => c.StaffProfileId == staffA.Id).CheckedIn);
        Assert.True(roster.Single(c => c.StaffProfileId == staffB.Id).CheckedIn);
    }

    // ── T037: 5 incorrect PIN attempts within 2 minutes locks A for 10 minutes; B unaffected;
    //    the triggering request and a request during active lockout return the identical shape (CHK008) ──

    [Fact]
    public async Task CheckIn_FiveIncorrectAttempts_LocksCaregiverFor10Minutes_OtherCaregiverUnaffected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Lockout Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staffA = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111", "Alice");
        var staffB = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2222", "Bob");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        for (var i = 0; i < 4; i++)
        {
            var response = await CheckInAsync(client, deviceToken, staffA.Id, "0000");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("errors.pin.invalid", await response.Content.ReadAsStringAsync());
        }

        // The 5th (triggering) attempt returns the Locked shape, not Invalid (CHK008).
        var triggeringResponse = await CheckInAsync(client, deviceToken, staffA.Id, "0000");
        Assert.Equal(HttpStatusCode.Locked, triggeringResponse.StatusCode);
        var triggeringBody = await triggeringResponse.Content.ReadAsStringAsync();
        Assert.Contains("errors.pin.locked", triggeringBody);

        // A request arriving during the active lockout — even with the *correct* PIN — gets the
        // identical locked-response shape as the triggering request.
        var duringLockoutResponse = await CheckInAsync(client, deviceToken, staffA.Id, "1111");
        Assert.Equal(HttpStatusCode.Locked, duringLockoutResponse.StatusCode);
        Assert.Contains("errors.pin.locked", await duringLockoutResponse.Content.ReadAsStringAsync());

        // Caregiver B's card is entirely unaffected by A's lockout.
        var bResponse = await CheckInAsync(client, deviceToken, staffB.Id, "2222");
        Assert.Equal(HttpStatusCode.OK, bResponse.StatusCode);
    }

    // ── CHK007: 4 failures spaced just past the sliding window each time never triggers a lockout ──

    [Fact]
    public async Task CheckIn_FourFailuresSpacedPastSlidingWindow_NeverLocksOut()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SlidingWindow Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        for (var i = 0; i < 4; i++)
        {
            var response = await CheckInAsync(client, deviceToken, staff.Id, "0000");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("errors.pin.invalid", await response.Content.ReadAsStringAsync());

            // Backdate the streak's anchor past the 2-minute sliding window — no injectable
            // clock exists in this codebase (VerifyPinCommand uses DateTime.UtcNow directly),
            // so this simulates real elapsed time between attempts without an 8-minute test.
            var db = ResolveTenantDb(factory.Services, schema);
            var profile = await db.StaffProfiles.SingleAsync(p => p.Id == staff.Id);
            profile.PinFirstFailedAttemptAt = DateTime.UtcNow.AddMinutes(-3);
            await db.SaveChangesAsync();
        }

        // A correct attempt after 4 never-consecutive failures still succeeds — the streak reset
        // each time the window elapsed, so it never reached 5 within any single window.
        var finalResponse = await CheckInAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
    }

    // ── T038: check-in for a staffId that already has an open shift returns 409 ──

    [Fact]
    public async Task CheckIn_AlreadyCheckedIn_Returns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AlreadyIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, staff.Id, "1234")).StatusCode);
        var secondResponse = await CheckInAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Contains("errors.room_shifts.already_checked_in", await secondResponse.Content.ReadAsStringAsync());
    }

    // ── T039: check-out for a staffId with no open shift returns 409 ──

    [Fact]
    public async Task CheckOut_NotCheckedIn_Returns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NotIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CheckOutAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.room_shifts.not_checked_in", await response.Content.ReadAsStringAsync());
    }

    // ── T042: PATCH as director updates CheckedInAt/CheckedOutAt and emits a structured audit log ──

    [Fact]
    public async Task CorrectShift_AsDirector_UpdatesTimes_EmitsAuditLog()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Correct Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, staff.Id, "1234");

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var shift = await db.RoomShifts.SingleAsync(s => s.StaffProfileId == staff.Id);

        var correctedCheckInAt = DateTime.UtcNow.AddHours(-1);
        var correctedCheckOutAt = DateTime.UtcNow.AddMinutes(-1);
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/room-shifts/{shift.Id}")
        {
            Content = JsonContent.Create(new { checkedInAt = correctedCheckInAt, checkedOutAt = correctedCheckOutAt }),
        };
        patchRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", org.AccessToken);
        var response = await client.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var corrected = (await response.Content.ReadFromJsonAsync<RoomShiftCorrectionResponse>())!;
        Assert.Equal(correctedCheckInAt, corrected.CheckedInAt, TimeSpan.FromSeconds(1));
        Assert.Equal(correctedCheckOutAt, corrected.CheckedOutAt!.Value, TimeSpan.FromSeconds(1));

        Assert.Contains(factory.LogCapture.Entries, e =>
            e.Message.Contains("corrected by director", StringComparison.Ordinal) &&
            e.Message.Contains(shift.Id.ToString(), StringComparison.Ordinal));
    }

    // ── T043: deactivating a checked-in caregiver closes their shift immediately and rejects
    //    subsequent check-in regardless of PIN correctness (FR-024) ──

    [Fact]
    public async Task DeactivateCaregiver_WhileCheckedIn_ClosesShiftImmediately_RejectsSubsequentCheckIn()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DeactivateCheckedIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, staff.Id, "1234");

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var shift = await db.RoomShifts.SingleAsync(s => s.StaffProfileId == staff.Id);
        Assert.NotNull(shift.CheckedOutAt);
        Assert.Equal("deactivated", shift.ClosedReason);

        var checkInResponse = await CheckInAsync(client, deviceToken, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.Forbidden, checkInResponse.StatusCode);
        Assert.Contains("errors.staff.not_eligible_here", await checkInResponse.Content.ReadAsStringAsync());
    }

    // ── T044: a caregiver eligible at two locations can check in with the same PIN at either;
    //    removing eligibility at A blocks check-in there on the next attempt, B remains usable (FR-025) ──

    [Fact]
    public async Task CheckIn_CaregiverEligibleAtTwoLocations_SamePinWorksAtEither_RevokingOneDoesNotAffectOther()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"MultiLocation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", locationA.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", locationB.Id);

        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, locationA.Id);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, locationB.Id);
        await SetPinAsync(client, org.AccessToken, staff.Id, "1234");

        var (_, deviceTokenA) = await PairDeviceAsync(client, org.AccessToken, locationA.Id, groupA.Id, "111222");
        var (_, deviceTokenB) = await PairDeviceAsync(client, org.AccessToken, locationB.Id, groupB.Id, "333444");

        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceTokenA, staff.Id, "1234")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceTokenB, staff.Id, "1234")).StatusCode);

        // Eligibility is evaluated fresh per call, not cached — removing it at A blocks the
        // *next* attempt there while B remains usable.
        await UnassignEligibilityAsync(client, org.AccessToken, staff.Id, locationA.Id);

        var afterRevokeAtA = await CheckOutAsync(client, deviceTokenA, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevokeAtA.StatusCode);
        Assert.Contains("errors.staff.not_eligible_here", await afterRevokeAtA.Content.ReadAsStringAsync());

        var stillWorksAtB = await CheckOutAsync(client, deviceTokenB, staff.Id, "1234");
        Assert.Equal(HttpStatusCode.OK, stillWorksAtB.StatusCode);
    }

    // ── T061 (US5): confirm-administrator for a currently-checked-in caregiver sets the field ──

    [Fact]
    public async Task ConfirmAdministrator_CurrentlyCheckedIn_SetsAdministeredByStaffProfileId()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ConfirmAdmin Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, staff.Id, "1234");

        var response = await ConfirmAdministratorAsync(client, deviceToken, staff.Id, "1234", false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmAdministratorResponse>())!;
        Assert.Equal(staff.Id, body.AdministeredByStaffProfileId);
    }

    // ── T062: confirm-administrator for an eligible-but-not-checked-in caregiver returns 409, regardless of PIN correctness ──

    [Fact]
    public async Task ConfirmAdministrator_NotCheckedIn_Returns409_RegardlessOfPinCorrectness()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ConfirmAdminNotIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await ConfirmAdministratorAsync(client, deviceToken, staff.Id, "1234", false);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.room_shifts.not_checked_in", await response.Content.ReadAsStringAsync());
    }

    // ── T063: { skip: true } always succeeds with administeredByStaffProfileId: null ──

    [Fact]
    public async Task ConfirmAdministrator_Skip_AlwaysSucceeds_WithNullAdministeredBy()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ConfirmAdminSkip Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await ConfirmAdministratorAsync(client, deviceToken, null, null, true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmAdministratorResponse>())!;
        Assert.Null(body.AdministeredByStaffProfileId);
    }

    // ── T064: a failed confirm-administrator attempt counts toward the same staffId's shared lockout counter ──

    [Fact]
    public async Task ConfirmAdministrator_FailedAttempt_CountsTowardSameSharedLockoutCounterAsCheckIn()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ConfirmAdminShared Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, staff.Id, "1234");

        // 4 failed confirm-administrator attempts, then a 5th failed check-out attempt — if the
        // lockout counter is truly shared, this 5th attempt (regardless of which endpoint) locks.
        for (var i = 0; i < 4; i++)
        {
            var response = await ConfirmAdministratorAsync(client, deviceToken, staff.Id, "0000", false);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var fifthAttempt = await CheckOutAsync(client, deviceToken, staff.Id, "0000");
        Assert.Equal(HttpStatusCode.Locked, fifthAttempt.StatusCode);
        Assert.Contains("errors.pin.locked", await fifthAttempt.Content.ReadAsStringAsync());
    }
}
