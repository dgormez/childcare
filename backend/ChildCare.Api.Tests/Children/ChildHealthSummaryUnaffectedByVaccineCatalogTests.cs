using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Children;

/// <summary>Feature 013g, spec.md FR-014 (analyze finding E1): the caregiver-facing
/// health-summary read path (013c) must stay decoupled from this feature's picker/attachment
/// additions — no vaccineTypeId, no attachment field, no edit affordance leaks through it,
/// regardless of what a vaccine record carries. Mirrors feature 012's decoupling-test
/// precedent (BKR ratio vs. staff_schedules).</summary>
public class ChildHealthSummaryUnaffectedByVaccineCatalogTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task HealthSummary_DueSoonVaccineFlag_NeverExposesVaccineTypeIdOrAttachment()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var vaccineTypesResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        var vaccineTypeId = (await vaccineTypesResponse.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!.First().Id;

        var dueSoon = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("DTP", null, new DateOnly(2026, 1, 1), dueSoon, null, null, vaccineTypeId)));
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records/{record.Id}/attachment-upload-url", org.AccessToken,
            new CreateVaccineRecordAttachmentUploadUrlRequest("image/png")));

        var summaryResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-summary", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var rawJson = await summaryResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("vaccineTypeId", rawJson);
        Assert.DoesNotContain("attachmentDownloadUrl", rawJson);

        var summary = (await summaryResponse.Content.ReadFromJsonAsync<ChildHealthSummaryResponse>())!;
        var flag = Assert.Single(summary.DueSoonVaccines);
        Assert.Equal("DTP", flag.VaccineName);
        Assert.False(flag.IsOverdue);
    }
}
