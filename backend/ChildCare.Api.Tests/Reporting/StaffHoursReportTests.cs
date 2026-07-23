using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>
/// Feature 028/US4 (FR-016/FR-017/FR-018/FR-019/FR-020): the medewerkersbeleid subsidy report —
/// aggregation, exclusions, zero-division safety, CSV parity, and the director-only access
/// boundary.
/// </summary>
public class StaffHoursReportTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task SeedAttendanceRecordAsync(Guid tenantId, Guid childId, Guid locationId, DateOnly date, DateTime checkInAt, DateTime? checkOutAt)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            ChildId = childId,
            LocationId = locationId,
            Date = date,
            Status = AttendanceStatus.Present,
            CheckInAt = checkInAt,
            CheckOutAt = checkOutAt,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedTimeEntryAsync(Guid tenantId, Guid staffProfileId, Guid locationId, StaffTimeEntryFunction function, DateTime clockedInAt, DateTime? clockedOutAt)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        db.StaffTimeEntries.Add(new StaffTimeEntry
        {
            StaffProfileId = staffProfileId,
            LocationId = locationId,
            Function = function,
            ClockedInAt = clockedInAt,
            ClockedOutAt = clockedOutAt,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Report_ComputesChildHoursAndStaffHoursByFunction_ExcludingOpenRecords()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Report Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var periodStart = new DateOnly(2026, 6, 1);
        var periodEnd = new DateOnly(2026, 6, 30);

        // 8 hours of child presence, closed.
        await SeedAttendanceRecordAsync(org.Organisation.Id, child.Id, location.Id, new DateOnly(2026, 6, 10),
            new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 10, 16, 0, 0, DateTimeKind.Utc));

        // An open attendance record inside the period — excluded (no known duration).
        await SeedAttendanceRecordAsync(org.Organisation.Id, child.Id, location.Id, new DateOnly(2026, 6, 11),
            new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc), null);

        // 6 hours of kinderbegeleider time, closed.
        await SeedTimeEntryAsync(org.Organisation.Id, staff.Id, location.Id, StaffTimeEntryFunction.Kinderbegeleider,
            new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc));

        // An open time entry inside the period — excluded (FR-019).
        await SeedTimeEntryAsync(org.Organisation.Id, staff.Id, location.Id, StaffTimeEntryFunction.Kinderbegeleider,
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc), null);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/reports/staff-hours?locationId={location.Id}&from={periodStart:yyyy-MM-dd}&to={periodEnd:yyyy-MM-dd}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<StaffHoursReportResponse>())!;

        Assert.Equal(8m, report.TotalChildHours);
        var kinderbegeleiderRow = report.ByFunction.Single(f => f.Function == "kinderbegeleider");
        Assert.Equal(6m, kinderbegeleiderRow.TotalStaffHours);
        Assert.NotNull(kinderbegeleiderRow.Ratio);
        Assert.Equal(8m / 6m, kinderbegeleiderRow.Ratio!.Value);

        var logistiekRow = report.ByFunction.Single(f => f.Function == "logistiek");
        Assert.Equal(0m, logistiekRow.TotalStaffHours);
        Assert.Null(logistiekRow.Ratio);
    }

    [Fact]
    public async Task Report_ZeroTimeEntries_ReturnsZeroHoursAndNullRatio_NotError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Report Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/reports/staff-hours?locationId={location.Id}&from=2026-06-01&to=2026-06-30", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<StaffHoursReportResponse>())!;

        Assert.Equal(0m, report.TotalChildHours);
        Assert.All(report.ByFunction, f => Assert.Equal(0m, f.TotalStaffHours));
        Assert.All(report.ByFunction, f => Assert.Null(f.Ratio));
    }

    [Fact]
    public async Task Export_CsvRowsSumToSameTotalAsOnScreenReport()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Report Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        await SeedTimeEntryAsync(org.Organisation.Id, staff.Id, location.Id, StaffTimeEntryFunction.Kinderbegeleider,
            new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc));
        await SeedTimeEntryAsync(org.Organisation.Id, staff.Id, location.Id, StaffTimeEntryFunction.Kinderbegeleider,
            new DateTime(2026, 6, 12, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc));

        var reportResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/reports/staff-hours?locationId={location.Id}&from=2026-06-01&to=2026-06-30", org.AccessToken));
        var report = (await reportResponse.Content.ReadFromJsonAsync<StaffHoursReportResponse>())!;
        var onScreenTotal = report.ByFunction.Single(f => f.Function == "kinderbegeleider").TotalStaffHours;

        var exportResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/reports/staff-hours/export?locationId={location.Id}&from=2026-06-01&to=2026-06-30", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        var csv = await exportResponse.Content.ReadAsStringAsync();

        var dataLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
        var csvTotal = dataLines.Sum(line => decimal.Parse(line.Split(',').Last(), System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(onScreenTotal, csvTotal);
    }

    [Fact]
    public async Task StaffHoursReport_RejectsStaffAuthenticatedRequest()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Report Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new ChildCare.Contracts.Requests.CreateStaffProfileRequest("Jane", "Doe", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) && e.Message.Contains(email, StringComparison.Ordinal));
        var match = System.Text.RegularExpressions.Regex.Match(entry.Message, @"token=([^&\s]+)");
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new ChildCare.Contracts.Requests.AcceptStaffInvitationRequest(org.Organisation.Slug, match.Groups[1].Value, "password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var reportResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/reports/staff-hours?locationId={location.Id}&from=2026-06-01&to=2026-06-30", session.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, reportResponse.StatusCode);

        var exportResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/reports/staff-hours/export?locationId={location.Id}&from=2026-06-01&to=2026-06-30", session.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }
}
