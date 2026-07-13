using System.Net;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.HealthRecords;

/// <summary>User Story 2 (spec.md FR-004): reject an invalid record type, missing
/// title/description, and a validUntil before validFrom.</summary>
public class HealthRecordValidationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task CreateHealthRecord_InvalidRecordType_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("not_a_real_type", "Title", "Description", null, null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateHealthRecord_MissingTitle_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "", "Description", null, null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateHealthRecord_MissingDescription_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Title", "", null, null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateHealthRecord_ValidUntilBeforeValidFrom_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Title", "Description", new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
