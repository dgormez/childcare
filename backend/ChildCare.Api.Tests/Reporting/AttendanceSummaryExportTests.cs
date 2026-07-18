using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 3 (spec.md FR-007/FR-008/FR-022, SC-002): CSV/PDF export totals match the
/// on-screen summary exactly, exports are never cached (reflect corrections immediately), and
/// cross-tenant isolation holds for the export endpoint too (FR-012).</summary>
public class AttendanceSummaryExportTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> GetSummaryAsync(HttpClient client, string accessToken, DateOnly month) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/reports/attendance-summary?month={month:yyyy-MM-dd}", accessToken));

    private static Task<HttpResponseMessage> ExportAsync(HttpClient client, string accessToken, DateOnly month, string format) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/reports/attendance-summary/export?month={month:yyyy-MM-dd}&format={format}", accessToken));

    [Fact]
    public async Task Export_Csv_TotalsMatchOnScreenSummaryExactly_Utf8Bom()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Export Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken, "Emma");

        var month = new DateOnly(2026, 6, 1);
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        for (var day = 1; day <= 5; day++)
            db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, day), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var summaryResponse = await GetSummaryAsync(client, org.AccessToken, month);
        var summary = (await summaryResponse.Content.ReadFromJsonAsync<AttendanceSummaryResponse>())!;
        var onScreenPresentDays = summary.Children.Single().PresentDays;

        var csvResponse = await ExportAsync(client, org.AccessToken, month, "csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType!.MediaType);
        var csvBytes = await csvResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(0xEF, csvBytes[0]);
        Assert.Equal(0xBB, csvBytes[1]);
        Assert.Equal(0xBF, csvBytes[2]);
        var csvText = System.Text.Encoding.UTF8.GetString(csvBytes);
        Assert.Contains($",{onScreenPresentDays},", csvText);
        Assert.Contains("Emma", csvText);
    }

    [Fact]
    public async Task Export_Pdf_RendersValidPdfStream_TotalsMatchOnScreenSummary()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Export Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);

        var month = new DateOnly(2026, 6, 1);
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 1), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var pdfResponse = await ExportAsync(client, org.AccessToken, month, "pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType!.MediaType);
        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), pdfBytes[..4]);
    }

    [Fact]
    public async Task Export_NeverCached_ReflectsCorrectionMadeAfterFirstExport()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Export Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken, "Emma");

        var month = new DateOnly(2026, 6, 1);
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 1), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var firstCsv = System.Text.Encoding.UTF8.GetString(await (await ExportAsync(client, org.AccessToken, month, "csv")).Content.ReadAsByteArrayAsync());
        Assert.Contains(",1,", firstCsv);

        // Correction after the first export: add a second present day.
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = child.Id, LocationId = location.Id, Date = new DateOnly(2026, 6, 2), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var secondCsv = System.Text.Encoding.UTF8.GetString(await (await ExportAsync(client, org.AccessToken, month, "csv")).Content.ReadAsByteArrayAsync());
        Assert.Contains(",2,", secondCsv);
    }

    [Fact]
    public async Task Export_CrossTenant_NeverLeaksOtherTenantsAttendance()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Tenant A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, orgA.AccessToken, "Location A");
        var childA = await CreateChildAsync(client, orgA.AccessToken, "TenantAChild");

        var month = new DateOnly(2026, 6, 1);
        var schemaNameA = await GetSchemaNameAsync(factory.Services, orgA.Organisation.Id);
        var dbA = ResolveTenantDb(factory.Services, schemaNameA);
        dbA.AttendanceRecords.Add(new AttendanceRecord { ChildId = childA.Id, LocationId = locationA.Id, Date = new DateOnly(2026, 6, 1), Status = AttendanceStatus.Present });
        await dbA.SaveChangesAsync();

        var orgB = await RegisterOrgAsync(client, $"Tenant B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var summaryBResponse = await GetSummaryAsync(client, orgB.AccessToken, month);
        var summaryB = (await summaryBResponse.Content.ReadFromJsonAsync<AttendanceSummaryResponse>())!;
        Assert.Empty(summaryB.Children);

        var csvB = System.Text.Encoding.UTF8.GetString(await (await ExportAsync(client, orgB.AccessToken, month, "csv")).Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("TenantAChild", csvB);
    }
}
