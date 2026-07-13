using System.Net;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 1 (spec.md FR-001): reject missing vaccine name and a future administered date.</summary>
public class VaccineRecordValidationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task CreateVaccineRecord_MissingVaccineName_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("", null, new DateOnly(2026, 1, 1), null, null, null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateVaccineRecord_AdministeredOnInFuture_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("DTP", null, futureDate, null, null, null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
