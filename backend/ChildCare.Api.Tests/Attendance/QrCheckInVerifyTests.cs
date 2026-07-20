using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>
/// Feature 021 — User Story 2, scan-verification half (spec.md FR-007–FR-011, FR-014, FR-019).
/// </summary>
public class QrCheckInVerifyTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private record Fixture(
        HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location,
        ChildResponse Child, string ParentToken, string DeviceToken);

    private async Task<Fixture> SetupEnabledLocationWithLinkedChildAsync(string orgLabel)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"{orgLabel} {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(true)));

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        return new Fixture(client, org, location, child, parentToken, deviceToken);
    }

    private static Task<HttpResponseMessage> IssueCodeAsync(Fixture f) =>
        f.Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/parent/attendance/qr-code", f.ParentToken, new IssueCheckInCodeRequest(f.Child.Id)));

    private static Task<HttpResponseMessage> VerifyCodeAsync(Fixture f, string code) =>
        f.Client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/attendance/qr-code/verify", f.DeviceToken, new VerifyCheckInCodeRequest(code)));

    // ── T022: a valid scan produces the same shape a manual tap produces (FR-008) ────

    [Fact]
    public async Task Verify_ValidCode_ChecksInChild_MatchesManualTapShape()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Happy Org");
        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;

        var response = await VerifyCodeAsync(f, issued.Code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<VerifyCheckInCodeResponse>())!;
        Assert.Equal("check-in", body.Direction);
        Assert.Equal(f.Child.Id, body.Attendance.ChildId);
        Assert.Equal(f.Location.Id, body.Attendance.LocationId);
        Assert.Equal("present", body.Attendance.Status);
        Assert.NotNull(body.Attendance.CheckInAt);
    }

    // ── T028: a second scan of a fresh code for an already-checked-in child checks out (FR-009) ──

    [Fact]
    public async Task Verify_SecondFreshCode_ChecksOutChild()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Toggle Org");
        var firstCode = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;
        var checkInResponse = await VerifyCodeAsync(f, firstCode.Code);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);

        var secondCode = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;
        var checkOutResponse = await VerifyCodeAsync(f, secondCode.Code);

        Assert.Equal(HttpStatusCode.OK, checkOutResponse.StatusCode);
        var body = (await checkOutResponse.Content.ReadFromJsonAsync<VerifyCheckInCodeResponse>())!;
        Assert.Equal("check-out", body.Direction);
        Assert.NotNull(body.Attendance.CheckOutAt);
    }

    // ── T023 (FR-019): re-verifying the same already-consumed code is rejected, not re-toggled ──

    [Fact]
    public async Task Verify_SameCodeTwice_SecondAttemptReturns409AlreadyUsed()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Cooldown Org");
        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;

        var first = await VerifyCodeAsync(f, issued.Code);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await VerifyCodeAsync(f, issued.Code);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("errors.qrCheckIn.already_used", await second.Content.ReadAsStringAsync());

        // The child must remain checked in — the second scan did not toggle a check-out.
        var todayResponse = await f.Client.SendAsync(DeviceRequest(HttpMethod.Get, "/api/attendance/today", f.DeviceToken));
        var records = (await todayResponse.Content.ReadFromJsonAsync<List<AttendanceRecordResponse>>())!;
        Assert.Single(records, r => r.ChildId == f.Child.Id && r.Status == "present");
    }

    // ── T025 (FR-010): a code for a child not enrolled at the scanning device's location ──

    [Fact]
    public async Task Verify_ChildNotEnrolledAtScanningLocation_Returns403WrongLocation()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Wrong Location Org");
        var otherLocation = await CreateLocationAsync(f.Client, f.Org.AccessToken, "Location B");
        var otherGroup = await CreateGroupAsync(f.Client, f.Org.AccessToken, "Group B", otherLocation.Id);
        var (_, otherDeviceToken) = await PairDeviceAsync(f.Client, f.Org.AccessToken, otherLocation.Id, otherGroup.Id);

        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;

        var response = await f.Client.SendAsync(DeviceRequest(
            HttpMethod.Post, "/api/attendance/qr-code/verify", otherDeviceToken, new VerifyCheckInCodeRequest(issued.Code)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.qrCheckIn.wrong_location", await response.Content.ReadAsStringAsync());

        var todayResponse = await f.Client.SendAsync(DeviceRequest(HttpMethod.Get, "/api/attendance/today", f.DeviceToken));
        var records = (await todayResponse.Content.ReadFromJsonAsync<List<AttendanceRecordResponse>>())!;
        Assert.Empty(records);
    }

    // ── T026 (FR-007): a tampered code is rejected ────────────────────────────────────

    [Fact]
    public async Task Verify_TamperedCode_Returns401InvalidCode()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Tamper Org");
        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;

        var tampered = issued.Code[..^1] + (issued.Code[^1] == 'A' ? 'B' : 'A');
        var response = await VerifyCodeAsync(f, tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("errors.qrCheckIn.invalid_code", await response.Content.ReadAsStringAsync());
    }

    // ── T027 (FR-014/SC-004): a QR-originated check-in produces identical BKR output to a manual tap ──

    [Fact]
    public async Task Verify_QrOriginatedCheckIn_ProducesIdenticalBkrOutputToManualTap()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Parity Org");
        var manualChild = await CreateAndActivateSecondChildAsync(f);

        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;
        await VerifyCodeAsync(f, issued.Code);
        await CheckInChildAsync(f.Client, f.DeviceToken, manualChild.Id, Application.Common.BelgianCalendarDay.Today());

        var bkrResponse = await GetBkrAsync(f.Client, f.DeviceToken, f.Location.Id);
        var bkr = (await bkrResponse.Content.ReadFromJsonAsync<BkrRatioResponse>())!;

        Assert.Equal(2, bkr.PresentCount);
    }

    private async Task<ChildResponse> CreateAndActivateSecondChildAsync(Fixture f)
    {
        var child = await ChildEventTestSupport.CreateChildAsync(f.Client, f.Org.AccessToken, "Second");
        await CreateAndActivateContractAsync(f.Client, f.Org.AccessToken, child.Id, f.Location.Id);
        return child;
    }

    // ── T024 (FR-006/FR-011): a code expires 30 seconds after issuance ────────────────

    [Fact]
    public async Task Verify_ExpiredCode_Returns410CodeExpired()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Expiry Org");
        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;

        await Task.Delay(TimeSpan.FromSeconds(31));

        var response = await VerifyCodeAsync(f, issued.Code);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Contains("errors.qrCheckIn.code_expired", await response.Content.ReadAsStringAsync());
    }

    // ── T046 (SC-003): server-side issuance→verify→committed-write latency stays well within
    // the 10-second scan-to-confirmation budget — client-side camera decode time is a separate,
    // manual quickstart timing observation (tasks.md T043), not automatable here.

    [Fact]
    public async Task Verify_EndToEndServerLatency_WellWithinTenSecondBudget()
    {
        var f = await SetupEnabledLocationWithLinkedChildAsync("QR Verify Latency Org");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var issued = (await (await IssueCodeAsync(f)).Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;
        var response = await VerifyCodeAsync(f, issued.Code);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Issuance + verification took {stopwatch.Elapsed.TotalSeconds:F2}s, expected well under 10s (SC-003).");
    }
}
