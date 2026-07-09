using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 1 (T011/T012/T015/T015a): check-in happy path, duplicate conflict,
/// closure-day rejection, and the absent-to-present transition.</summary>
public class CheckInTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 1, 5);

    [Fact]
    public async Task CheckIn_HappyPath_CreatesPresentRecord_Returns201()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.Equal("present", body.Status);
        Assert.NotNull(body.CheckInAt);
        Assert.Null(body.CheckOutAt);
    }

    [Fact]
    public async Task CheckIn_Duplicate_ReturnsConflict_NoSecondRecordCreated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckIn Dup Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var first = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var list = await ListAttendanceAsync(client, org.AccessToken, location.Id, Monday);
        var page = (await list.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        Assert.Single(page.Items, r => r.ChildId == child.Id);
    }

    [Fact]
    public async Task CheckIn_AgainstClosureRecord_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckIn Closure Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        // FR-015: no API creates a closure record in this feature (feature 011's job) — seed
        // one directly via the tenant schema, mirroring how other tests reach into the DB for
        // setup with no API surface (e.g. ChildEventTestSupport's PushToken seeding).
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        db.AttendanceRecords.Add(new Domain.Entities.AttendanceRecord
        {
            ChildId = child.Id,
            LocationId = location.Id,
            Date = Monday,
            Status = Domain.Enums.AttendanceStatus.Closure,
        });
        await db.SaveChangesAsync();

        var response = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CheckIn_AgainstAbsentRecord_TransitionsToPresent_Returns200()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CheckIn Transition Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var absence = await MarkAbsentAsDeviceAsync(client, deviceToken, child.Id, location.Id, Monday, justified: false);
        Assert.Equal(HttpStatusCode.Created, absence.StatusCode);

        var checkIn = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        var body = (await checkIn.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
        Assert.Equal("present", body.Status);
        Assert.NotNull(body.CheckInAt);
        Assert.Null(body.AbsenceJustified);

        var list = await ListAttendanceAsync(client, org.AccessToken, location.Id, Monday);
        var page = (await list.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
        Assert.Single(page.Items, r => r.ChildId == child.Id);
    }
}
