using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 2 (spec.md FR-006/FR-007/FR-008): a typed, non-catalog vaccine name is
/// remembered per-tenant, reused (including under a case/whitespace/diacritic-insensitive
/// concurrent race), and never shared across tenants.</summary>
public class CustomVaccineEntryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateVaccineRecordRequest CustomNameRequest(string name) =>
        new(name, null, new DateOnly(2026, 6, 1), null, null, null, null);

    [Fact]
    public async Task TypedNonCatalogName_IsRememberedAndReusedForAnotherChildInSameTenant()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");

        var first = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/vaccine-records", org.AccessToken,
            CustomNameRequest("Rabiës")));
        Assert.Equal(System.Net.HttpStatusCode.Created, first.StatusCode);

        var entriesResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-custom-entries", org.AccessToken));
        var entries = (await entriesResponse.Content.ReadFromJsonAsync<List<CustomVaccineEntryResponse>>())!;
        var entry = Assert.Single(entries);
        Assert.Equal("Rabiës", entry.Name);

        // A second child, same tenant, same custom name — no second entry created.
        var second = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childB.Id}/vaccine-records", org.AccessToken,
            CustomNameRequest("Rabiës")));
        Assert.Equal(System.Net.HttpStatusCode.Created, second.StatusCode);

        var entriesAfter = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-custom-entries", org.AccessToken)))
            .Content.ReadFromJsonAsync<List<CustomVaccineEntryResponse>>())!;
        Assert.Single(entriesAfter);
    }

    [Fact]
    public async Task ConcurrentNearDuplicateNames_ResolveToASingleRememberedEntry()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");

        // Two near-simultaneous writes with case/whitespace/diacritic-different spellings of the
        // same name — exercises the unique-index dedupe under a race (research.md R3), not just
        // sequential dedupe.
        var task1 = client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/vaccine-records", org.AccessToken,
            CustomNameRequest("Rabiës")));
        var task2 = client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childB.Id}/vaccine-records", org.AccessToken,
            CustomNameRequest("rabies ")));
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.Equal(System.Net.HttpStatusCode.Created, r.StatusCode));

        var entries = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-custom-entries", org.AccessToken)))
            .Content.ReadFromJsonAsync<List<CustomVaccineEntryResponse>>())!;
        Assert.Single(entries); // both variants resolved to the same remembered entry
    }

    [Fact]
    public async Task CustomEntry_NeverVisibleToADifferentTenant()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Vaccine Org A {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var childA = await CreateChildAsync(client, orgA.AccessToken);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childA.Id}/vaccine-records", orgA.AccessToken,
            CustomNameRequest("Rabiës")));

        var orgB = await RegisterOrgAsync(client, $"Vaccine Org B {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var entriesForB = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-custom-entries", orgB.AccessToken)))
            .Content.ReadFromJsonAsync<List<CustomVaccineEntryResponse>>())!;

        Assert.Empty(entriesForB);
    }
}
