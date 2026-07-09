using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 1 (T013/T015b): check-out happy path and the not-found idempotency
/// rule for a missing/already-checked-out record.</summary>
public class CheckOutTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 1, 5);

    [Fact]
    public async Task CheckOut_HappyPath_SetsCheckOutAt_Returns200()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckOut Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        var response = await CheckOutChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.NotNull(body.CheckOutAt);
    }

    [Fact]
    public async Task CheckOut_WithNoMatchingPresentRecord_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckOut NotFound Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        // Never checked in.
        var response = await CheckOutChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckOut_Twice_SecondAttemptReturnsNotFound_FirstTimeUnchanged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckOut Twice Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        var first = await CheckOutChildAsync(client, deviceToken, child.Id, Monday);
        var firstBody = (await first.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        var second = await CheckOutChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);

        var list = await ListAttendanceAsync(client, org.AccessToken, location.Id, Monday);
        var page = (await list.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        var record = page.Items.Single(r => r.ChildId == child.Id);
        Assert.Equal(firstBody.CheckOutAt, record.CheckOutAt);
    }
}
