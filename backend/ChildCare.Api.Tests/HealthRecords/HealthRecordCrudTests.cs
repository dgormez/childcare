using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.HealthRecords;

/// <summary>User Story 2 (spec.md): director create/list/update/delete health record happy
/// path, across all five record types.</summary>
public class HealthRecordCrudTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Theory]
    [InlineData("allergy")]
    [InlineData("chronic_condition")]
    [InlineData("medication_standing")]
    [InlineData("doctor_note")]
    [InlineData("other")]
    public async Task CreateHealthRecord_EachRecordType_ReturnsCreatedAndListed(string recordType)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest(recordType, "Title", "Description", null, null)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;
        Assert.Equal(recordType, record.RecordType);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<HealthRecordResponse>>())!;
        Assert.Contains(list, r => r.Id == record.Id);
    }

    [Fact]
    public async Task UpdateHealthRecord_ChangesTitleAndDescription()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Peanut allergy", "Confirmed by allergist.", null, null)));
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var updated = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{child.Id}/health-records/{record.Id}", org.AccessToken,
            new UpdateHealthRecordRequest("allergy", "Peanut & tree nut allergy", "Confirmed by allergist, carries EpiPen.", null, null)));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedRecord = (await updated.Content.ReadFromJsonAsync<HealthRecordResponse>())!;
        Assert.Equal("Peanut & tree nut allergy", updatedRecord.Title);
    }

    [Fact]
    public async Task DeleteHealthRecord_SoftDeletes_NoLongerListed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("other", "Title", "Description", null, null)));
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var deleted = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{child.Id}/health-records/{record.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<HealthRecordResponse>>())!;
        Assert.Empty(list);
    }
}
