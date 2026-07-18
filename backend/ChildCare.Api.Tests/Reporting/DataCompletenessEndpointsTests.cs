using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 5 (spec.md FR-011, research.md R7): the four data-completeness checks —
/// missing pickup contact, overdue vaccine, missing staff qualification, missing staff PIN — and
/// tenant isolation.</summary>
public class DataCompletenessEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> GetCompletenessAsync(HttpClient client, string accessToken) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/reports/data-completeness", accessToken));

    private static Task<HttpResponseMessage> CreateVaccineRecordAsync(
        HttpClient client, string accessToken, Guid childId, string vaccineName, DateOnly administeredOn, DateOnly? nextDueDate) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/vaccine-records", accessToken,
            new CreateVaccineRecordRequest(vaccineName, null, administeredOn, nextDueDate, null, null)));

    [Fact]
    public async Task DataCompleteness_FlagsAllFourGapTypes()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Completeness Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Attendance is how a child enters this report's scope (research.md R7's own read
        // model reads AttendanceRecord to find children ever attending a scoped location) —
        // check the child in once via the device flow to establish that.
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        // Gap 1: no pickup contact at all.
        var noPickupChild = await CreateChildAsync(client, org.AccessToken, "NoPickupChild");
        await ChildCare.Api.Tests.AttendanceTestSupport.CheckInChildAsync(client, deviceToken, noPickupChild.Id, today);

        // Gap 2: overdue vaccine.
        var overdueVaccineChild = await CreateChildAsync(client, org.AccessToken, "OverdueVaccineChild");
        await ChildCare.Api.Tests.AttendanceTestSupport.CheckInChildAsync(client, deviceToken, overdueVaccineChild.Id, today);
        await CreateVaccineRecordAsync(client, org.AccessToken, overdueVaccineChild.Id, "DTP", today.AddDays(-100), today.AddDays(-5));

        // Gap 3: staff missing qualification level. CreateStaffProfileCommandValidator (and
        // UpdateStaffProfileCommandValidator) both reject a null QualificationLevel for a Staff
        // role at every application entry point — this state can only arise from data outside
        // those paths (a future bulk import, a legacy record), so it's seeded directly here,
        // matching this codebase's own precedent for states the API can't produce (e.g.
        // PaymentReminderTests' BackdateDueDateAsync).
        var noQualStaff = await CreateStaffAsync(client, org.AccessToken, "NoQual");
        await AssignEligibilityAsync(client, org.AccessToken, noQualStaff.Id, location.Id);
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var trackedNoQualStaff = await db.StaffProfiles.FirstAsync(p => p.Id == noQualStaff.Id);
        trackedNoQualStaff.QualificationLevel = null;
        await db.SaveChangesAsync();

        // Gap 4: staff with no PIN set.
        var noPinStaff = await CreateStaffAsync(client, org.AccessToken, "NoPin");
        await AssignEligibilityAsync(client, org.AccessToken, noPinStaff.Id, location.Id);

        var response = await GetCompletenessAsync(client, org.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<DataCompletenessResponse>())!;

        Assert.Contains(body.Flags, f => f.Type == "missing_pickup_contact" && f.SubjectId == noPickupChild.Id);
        Assert.Contains(body.Flags, f => f.Type == "overdue_vaccine" && f.SubjectId == overdueVaccineChild.Id);
        Assert.Contains(body.Flags, f => f.Type == "missing_qualification" && f.SubjectId == noQualStaff.Id);
        Assert.Contains(body.Flags, f => f.Type == "missing_pin" && f.SubjectId == noPinStaff.Id);
    }

    [Fact]
    public async Task DataCompleteness_NoGaps_ReturnsEmptyFlags()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Completeness Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        await CreateLocationAsync(client, org.AccessToken, "Main");

        var response = await GetCompletenessAsync(client, org.AccessToken);
        var body = (await response.Content.ReadFromJsonAsync<DataCompletenessResponse>())!;
        Assert.Empty(body.Flags);
    }

    [Fact]
    public async Task DataCompleteness_CrossTenant_NeverLeaksOtherTenantsFlags()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Tenant A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, orgA.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, orgA.AccessToken, "Group A", locationA.Id);
        var (_, deviceTokenA) = await PairDeviceAsync(client, orgA.AccessToken, locationA.Id, groupA.Id);
        var childA = await CreateChildAsync(client, orgA.AccessToken, "TenantAChild");
        await ChildCare.Api.Tests.AttendanceTestSupport.CheckInChildAsync(client, deviceTokenA, childA.Id, DateOnly.FromDateTime(DateTime.UtcNow));

        var orgB = await RegisterOrgAsync(client, $"Tenant B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var responseB = await GetCompletenessAsync(client, orgB.AccessToken);
        var bodyB = (await responseB.Content.ReadFromJsonAsync<DataCompletenessResponse>())!;
        Assert.DoesNotContain(bodyB.Flags, f => f.SubjectId == childA.Id);
    }
}
