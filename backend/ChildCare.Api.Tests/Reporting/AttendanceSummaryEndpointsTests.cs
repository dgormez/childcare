using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 3 (spec.md FR-006, data-model.md's Edge Case): monthly attendance
/// aggregation per child/group/location, and correct attribution across a mid-month location
/// change.</summary>
public class AttendanceSummaryEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> GetSummaryAsync(HttpClient client, string accessToken, DateOnly month, Guid? locationId = null) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get,
            $"/api/reports/attendance-summary?month={month:yyyy-MM-dd}" + (locationId is null ? "" : $"&locationId={locationId}"), accessToken));

    [Fact]
    public async Task AttendanceSummary_AggregatesStatusCounts_RollsUpPerGroupAndLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Attendance Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);

        var month = new DateOnly(2026, 6, 1);
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);

        db.ChildGroupAssignments.Add(new ChildGroupAssignment { ChildId = child.Id, GroupId = group.Id, StartDate = month, EndDate = null });

        for (var day = 1; day <= 18; day++)
            db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, day), Status = AttendanceStatus.Present });
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 19), Status = AttendanceStatus.Absent, AbsenceJustified = true });
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 20), Status = AttendanceStatus.Absent, AbsenceJustified = false });
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 21), Status = AttendanceStatus.Closure });
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 22), Status = AttendanceStatus.Closure });
        await db.SaveChangesAsync();

        var response = await GetSummaryAsync(client, org.AccessToken, month);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceSummaryResponse>())!;

        var row = Assert.Single(body.Children);
        Assert.Equal(18, row.PresentDays);
        Assert.Equal(1, row.AbsentJustifiedDays);
        Assert.Equal(1, row.AbsentUnjustifiedDays);
        Assert.Equal(2, row.ClosureDays);
        Assert.Equal(group.Id, row.GroupId);

        var groupTotal = Assert.Single(body.GroupTotals, g => g.Id == group.Id);
        Assert.Equal(18, groupTotal.PresentDays);
        var locationTotal = Assert.Single(body.LocationTotals, l => l.Id == location.Id);
        Assert.Equal(18, locationTotal.PresentDays);
    }

    [Fact]
    public async Task AttendanceSummary_MidMonthLocationChange_AttributesEachDayCorrectly_NoDoubleCountOrDrop()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Attendance Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var child = await CreateChildAsync(client, org.AccessToken);

        var month = new DateOnly(2026, 6, 1);
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);

        for (var day = 1; day <= 10; day++)
            db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = locationA.Id, Date = new DateOnly(2026, 6, day), Status = AttendanceStatus.Present });
        for (var day = 11; day <= 18; day++)
            db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = locationB.Id, Date = new DateOnly(2026, 6, day), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var response = await GetSummaryAsync(client, org.AccessToken, month);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceSummaryResponse>())!;

        Assert.Equal(2, body.Children.Count);
        var rowA = Assert.Single(body.Children, r => r.LocationId == locationA.Id);
        var rowB = Assert.Single(body.Children, r => r.LocationId == locationB.Id);
        Assert.Equal(10, rowA.PresentDays);
        Assert.Equal(8, rowB.PresentDays);
        Assert.Equal(18, rowA.PresentDays + rowB.PresentDays);
    }

    [Fact]
    public async Task AttendanceSummary_MonthWithNoRecords_ReturnsValidEmptyResult()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Attendance Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await CreateLocationAsync(client, org.AccessToken, "Main");

        var response = await GetSummaryAsync(client, org.AccessToken, new DateOnly(2026, 1, 1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AttendanceSummaryResponse>())!;
        Assert.Empty(body.Children);
    }
}
