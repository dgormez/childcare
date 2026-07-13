using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>
/// Feature 013c FR-016/SC-004: vaccine and health record data must never appear in a bulk
/// child export or summary by default. No bulk export/email-summary feature exists in this
/// codebase yet (spec.md Assumptions' "Known limitation, by design") — this is a forward-looking
/// regression test proving the two existing "read many children at once" endpoints
/// (`GET /api/children` and `GET /api/children/{id}`) never embed this data, so whichever
/// future feature builds a real bulk export inherits an explicit opt-in requirement rather than
/// discovering vaccine/health data was already leaking through an existing endpoint.
/// </summary>
public class HealthDataExportExclusionTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private const string SensitiveTitle = "Extremely Sensitive Peanut Allergy Detail XYZ123";
    private const string SensitiveVaccineName = "ExtremelySensitiveVaccineNameXYZ123";

    [Fact]
    public async Task GetChildById_And_ListChildren_NeverIncludeVaccineOrHealthRecordData()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Export Exclusion Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", SensitiveTitle, "Description text.", null, null)));
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest(SensitiveVaccineName, null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null)));

        var getByIdResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        var getByIdBody = await getByIdResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SensitiveTitle, getByIdBody);
        Assert.DoesNotContain(SensitiveVaccineName, getByIdBody);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SensitiveTitle, listBody);
        Assert.DoesNotContain(SensitiveVaccineName, listBody);
    }
}
