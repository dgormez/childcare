using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 3 (T032/T033/T033a): absence-mark creation by caregiver or director,
/// duplicate conflict, and the race against a concurrent check-in.</summary>
public class MarkAbsentTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 1, 5);

    [Fact]
    public async Task MarkAbsent_ByCaregiver_Justified_CreatesRecordWithReason()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Absent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await MarkAbsentAsDeviceAsync(client, deviceToken, child.Id, location.Id, Monday, justified: true, "Sick, doctor's note");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.Equal("absent", body.Status);
        Assert.True(body.AbsenceJustified);
        Assert.Equal("Sick, doctor's note", body.AbsenceReason);
    }

    [Fact]
    public async Task MarkAbsent_ByDirector_Unjustified_CreatesRecord()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Absent Director Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await MarkAbsentAsDirectorAsync(client, org.AccessToken, child.Id, location.Id, Monday, justified: false);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.False(body.AbsenceJustified);
    }

    [Fact]
    public async Task MarkAbsent_Duplicate_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Absent Dup Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        await MarkAbsentAsDeviceAsync(client, deviceToken, child.Id, location.Id, Monday, justified: true);
        var second = await MarkAbsentAsDeviceAsync(client, deviceToken, child.Id, location.Id, Monday, justified: false);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // FR-005: a check-in and an absence-mark racing for the same child/location/date resolve via
    // the same unique constraint. Two valid interleavings exist, both non-500: either the
    // check-in's insert commits first (201) and the absence-mark's insert then conflicts (409),
    // or the absence-mark's insert commits first (201) and the check-in's insert then loses,
    // but — per FR-001a — transitions that just-created absent record to present (200) rather
    // than conflicting. Either way, exactly one record persists and its final status is always
    // "present" (FR-001a's transition guarantees this regardless of interleaving).
    [Fact]
    public async Task MarkAbsent_RacingAgainstCheckIn_ExactlyOneRecordPersists_EndsPresent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Absent Race Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var checkInTask = CheckInChildAsync(client, deviceToken, child.Id, Monday);
        var absentTask = MarkAbsentAsDeviceAsync(client, deviceToken, child.Id, location.Id, Monday, justified: true);
        await Task.WhenAll(checkInTask, absentTask);

        var checkInStatus = checkInTask.Result.StatusCode;
        var absentStatus = absentTask.Result.StatusCode;
        Assert.True(checkInStatus is HttpStatusCode.Created or HttpStatusCode.OK, $"Unexpected check-in status: {checkInStatus}");
        Assert.True(absentStatus is HttpStatusCode.Created or HttpStatusCode.Conflict, $"Unexpected absence-mark status: {absentStatus}");
        Assert.True(
            (checkInStatus == HttpStatusCode.Created && absentStatus == HttpStatusCode.Conflict)
            || (checkInStatus == HttpStatusCode.OK && absentStatus == HttpStatusCode.Created),
            $"Unexpected status combination: check-in={checkInStatus}, absence={absentStatus}");

        var list = await ListAttendanceAsync(client, org.AccessToken, location.Id, Monday);
        var page = (await list.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        var record = Assert.Single(page.Items, r => r.ChildId == child.Id);
        Assert.Equal("present", record.Status);
    }
}
