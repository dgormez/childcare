using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.IncidentReports.IncidentReportTestSupport;

namespace ChildCare.Api.Tests.IncidentReports;

/// <summary>User Story 3 (T044-T047): 24-hour immutability lock (FR-005/FR-006/FR-007).</summary>
public class IncidentReportImmutabilityTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task BackdateCreatedAtAsync(Guid orgId, Guid reportId, TimeSpan age)
    {
        var schema = await GetSchemaNameAsync(factory.Services, orgId);
        var db = ResolveTenantDb(factory.Services, schema);
        var report = await db.IncidentReports.SingleAsync(r => r.Id == reportId);
        report.CreatedAt = DateTime.UtcNow - age;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Update_OnReportOlderThan24h_ChangingLockedField_Returns409AndLeavesRecordUnchanged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var filed = (await (await FileIncidentReportAsync(client, deviceToken, child.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        await BackdateCreatedAtAsync(org.Organisation.Id, filed.Id, TimeSpan.FromHours(25));

        var response = await UpdateIncidentReportAsDeviceAsync(client, deviceToken, filed.Id, new UpdateIncidentReportRequest(
            null, null, "changed description", null, null, null, null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("errors.incident_reports.locked", await response.Content.ReadAsStringAsync());

        var reGet = (await (await GetIncidentReportAsDeviceAsync(client, deviceToken, filed.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Equal(filed.Description, reGet.Description);
    }

    [Fact]
    public async Task Update_OnReportOlderThan24h_OnlyFollowUp_SucceedsRegardlessOfAge()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var filed = (await (await FileIncidentReportAsync(client, deviceToken, child.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        await BackdateCreatedAtAsync(org.Organisation.Id, filed.Id, TimeSpan.FromDays(3));

        var response = await UpdateIncidentReportAsDirectorAsync(client, org.AccessToken, filed.Id, new UpdateIncidentReportRequest(
            null, null, null, null, null, null, null, null, null, null, null, "Doctor visit follow-up."));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Equal("Doctor visit follow-up.", updated.FollowUp);
        Assert.Equal(filed.Description, updated.Description);
    }

    [Fact]
    public async Task Update_OnReportYoungerThan24h_AcceptsAnyFieldChange()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var filed = (await (await FileIncidentReportAsync(client, deviceToken, child.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;

        var response = await UpdateIncidentReportAsDeviceAsync(client, deviceToken, filed.Id, new UpdateIncidentReportRequest(
            null, null, "corrected description", null, null, null, null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Equal("corrected description", updated.Description);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_OnReviewedReport_DoesNotResetReviewedAt()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var filed = (await (await FileIncidentReportAsync(client, deviceToken, child.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        var reviewed = (await (await GetIncidentReportAsDirectorAsync(client, org.AccessToken, filed.Id)).Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.NotNull(reviewed.ReviewedAt);

        var updateResponse = await UpdateIncidentReportAsDirectorAsync(client, org.AccessToken, filed.Id, new UpdateIncidentReportRequest(
            null, null, "corrected after review", null, null, null, null, null, null, null, null, null));
        var updated = (await updateResponse.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        // `reviewed` came from the same in-memory tracked entity GetIncidentReportQuery just
        // saved (full .NET tick precision); `updated` is a fresh read from Postgres, which
        // stores timestamptz at microsecond precision — an exact Assert.Equal is flaky against
        // that sub-microsecond rounding (same class of issue as feature 010's precedent). What
        // this test actually asserts is "unaffected by the edit," not bit-for-bit identity.
        Assert.True(Math.Abs((reviewed.ReviewedAt!.Value - updated.ReviewedAt!.Value).TotalMilliseconds) < 1);
    }
}
