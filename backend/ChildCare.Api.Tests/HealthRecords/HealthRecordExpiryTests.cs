using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.HealthRecords;

/// <summary>User Story 2 (spec.md FR-008): a record with `validUntil` in the past is still
/// returned by GET (never hidden), flagged `isExpired: true`.</summary>
public class HealthRecordExpiryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetHealthRecords_PastValidUntil_StillReturnedAsExpired()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var expired = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("medication_standing", "Old prescription", "No longer active.",
                new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1))));
        var expiredRecord = (await expired.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var current = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Peanut allergy", "Confirmed.", null, null)));
        var currentRecord = (await current.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-records", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<List<HealthRecordResponse>>())!;

        var expiredResult = list.Single(r => r.Id == expiredRecord.Id);
        Assert.True(expiredResult.IsExpired);

        var currentResult = list.Single(r => r.Id == currentRecord.Id);
        Assert.False(currentResult.IsExpired);
    }
}
