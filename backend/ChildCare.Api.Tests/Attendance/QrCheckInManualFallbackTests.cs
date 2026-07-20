using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>
/// Feature 021 — User Story 3 (manual tap-based check-in remains available and unaffected
/// everywhere, FR-004/SC-005). T040: proves the existing manual check-in/check-out endpoints
/// behave identically to their pre-feature-021 shape and status codes regardless of the
/// location's QrCheckInEnabled value — this feature must never regress the existing flow.
/// </summary>
public class QrCheckInManualFallbackTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location, string DeviceToken, ChildResponse Child)> SetupAsync(bool qrCheckInEnabled)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"QR Manual Fallback Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(qrCheckInEnabled)));
        var child = await ChildEventTestSupport.CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        return (client, org, location, deviceToken, child);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualCheckIn_ProducesIdenticalShapeRegardlessOfQrCheckInSetting(bool qrCheckInEnabled)
    {
        var (client, _, _, deviceToken, child) = await SetupAsync(qrCheckInEnabled);
        var today = Application.Common.BelgianCalendarDay.Today();

        var response = await CheckInChildAsync(client, deviceToken, child.Id, today);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.Equal(child.Id, body.ChildId);
        Assert.Equal("present", body.Status);
        Assert.NotNull(body.CheckInAt);
        Assert.Null(body.CheckOutAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualCheckOut_ProducesIdenticalShapeRegardlessOfQrCheckInSetting(bool qrCheckInEnabled)
    {
        var (client, _, _, deviceToken, child) = await SetupAsync(qrCheckInEnabled);
        var today = Application.Common.BelgianCalendarDay.Today();
        await CheckInChildAsync(client, deviceToken, child.Id, today);

        var response = await CheckOutChildAsync(client, deviceToken, child.Id, today);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.Equal("present", body.Status);
        Assert.NotNull(body.CheckOutAt);
    }
}
