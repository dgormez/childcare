using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 1 (spec.md): director create/list/update/delete vaccine record happy path.</summary>
public class VaccineRecordCrudTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateVaccineRecordRequest MinimalRequest() =>
        new("DTP", 2, new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 1), "Dr. Peeters", null);

    [Fact]
    public async Task CreateVaccineRecord_ThenListsAndAppearsMostRecentFirst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var older = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("Hep B", 1, new DateOnly(2026, 1, 1), null, null, null)));
        Assert.Equal(HttpStatusCode.Created, older.StatusCode);

        var newer = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken, MinimalRequest()));
        Assert.Equal(HttpStatusCode.Created, newer.StatusCode);
        var newerRecord = (await newer.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;
        Assert.Equal("DTP", newerRecord.VaccineName);
        Assert.NotNull(newerRecord.RecordedBy);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        Assert.Equal(2, list.Count);
        Assert.Equal("DTP", list[0].VaccineName); // most-recently-administered first (FR-003)
    }

    [Fact]
    public async Task UpdateVaccineRecord_ChangesNextDueDate()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken, MinimalRequest()));
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;

        var updated = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{child.Id}/vaccine-records/{record.Id}", org.AccessToken,
            new UpdateVaccineRecordRequest("DTP", 2, new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 1), "Dr. Peeters", null)));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedRecord = (await updated.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;
        Assert.Equal(new DateOnly(2026, 9, 1), updatedRecord.NextDueDate);
    }

    [Fact]
    public async Task DeleteVaccineRecord_SoftDeletes_NoLongerListed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken, MinimalRequest()));
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;

        var deleted = await client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/children/{child.Id}/vaccine-records/{record.Id}", org.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        Assert.Empty(list);
    }
}
