using System.Net;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 1: a childId that doesn't resolve within the tenant returns 404.</summary>
public class VaccineRecordNotFoundTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task CreateVaccineRecord_UnknownChildId_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{Guid.NewGuid()}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("DTP", null, new DateOnly(2026, 1, 1), null, null, null)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVaccineRecord_UnknownId_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{Guid.NewGuid()}/vaccine-records/{Guid.NewGuid()}", org.AccessToken,
            new UpdateVaccineRecordRequest("DTP", null, new DateOnly(2026, 1, 1), null, null, null)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
