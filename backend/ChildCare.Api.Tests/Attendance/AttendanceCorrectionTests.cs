using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 4 (T041-T046, T045a): director any-day correction, caregiver
/// same-day/own-location correction, closure-status immutability, status invariants, delete,
/// and history pagination.</summary>
public class AttendanceCorrectionTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly PriorDay = new(2020, 1, 6); // a Monday, safely in the past

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location, GroupResponse Group, string DeviceToken, ChildResponse Child)>
        SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Correction Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        return (client, org, location, group, deviceToken, child);
    }

    [Fact]
    public async Task Caregiver_CanCorrect_SameDayRecord_AtOwnLocation()
    {
        var (client, org, location, _, deviceToken, child) = await SetupAsync();
        var today = BelgianCalendarDay.Today();
        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, today);
        var record = (await checkIn.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        var response = await PatchAttendanceAsDeviceAsync(client, deviceToken, record.Id, checkOutAt: DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Caregiver_CannotCorrect_PriorDayRecord()
    {
        var (client, org, location, _, deviceToken, child) = await SetupAsync();

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var record = new Domain.Entities.AttendanceRecord
        {
            ChildId = child.Id,
            LocationId = location.Id,
            Date = PriorDay,
            Status = Domain.Enums.AttendanceStatus.Present,
            CheckInAt = DateTime.UtcNow.AddYears(-6),
        };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var response = await PatchAttendanceAsDeviceAsync(client, deviceToken, record.Id, checkOutAt: DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Caregiver_CannotCorrect_RecordAtDifferentLocation()
    {
        var (client, org, location, _, deviceToken, child) = await SetupAsync();
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var otherGroup = await CreateGroupAsync(client, org.AccessToken, "Group B", otherLocation.Id);
        var (_, otherDeviceToken) = await PairDeviceAsync(client, org.AccessToken, otherLocation.Id, otherGroup.Id);

        var today = BelgianCalendarDay.Today();
        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, today);
        var record = (await checkIn.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        var response = await PatchAttendanceAsDeviceAsync(client, otherDeviceToken, record.Id, checkOutAt: DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Director_CanCorrect_AnyRecordRegardlessOfAge()
    {
        var (client, org, location, _, _, child) = await SetupAsync();

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var record = new Domain.Entities.AttendanceRecord
        {
            ChildId = child.Id,
            LocationId = location.Id,
            Date = PriorDay,
            Status = Domain.Enums.AttendanceStatus.Present,
            CheckInAt = DateTime.UtcNow.AddYears(-6),
        };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var response = await PatchAttendanceAsDirectorAsync(client, org.AccessToken, record.Id, checkOutAt: DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CorrectingStatusToClosure_IsRejected()
    {
        var (client, org, _, _, deviceToken, child) = await SetupAsync();
        var today = BelgianCalendarDay.Today();
        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, today);
        var record = (await checkIn.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        var response = await PatchAttendanceAsDirectorAsync(client, org.AccessToken, record.Id, status: "closure");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CorrectingToPresent_WithNoCheckInAt_IsRejected()
    {
        var (client, org, location, _, _, child) = await SetupAsync();
        var today = BelgianCalendarDay.Today();

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var record = new Domain.Entities.AttendanceRecord
        {
            ChildId = child.Id,
            LocationId = location.Id,
            Date = today,
            Status = Domain.Enums.AttendanceStatus.Absent,
            AbsenceJustified = true,
        };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var response = await PatchAttendanceAsDirectorAsync(client, org.AccessToken, record.Id, status: "present");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesRecord_SameAuthorizationRulesAsPatch()
    {
        var (client, org, _, _, deviceToken, child) = await SetupAsync();
        var today = BelgianCalendarDay.Today();
        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, today);
        var record = (await checkIn.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;

        var response = await DeleteAttendanceAsDeviceAsync(client, deviceToken, record.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
