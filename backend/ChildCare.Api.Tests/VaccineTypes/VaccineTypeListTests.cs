using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.VaccineTypes;

/// <summary>User Story 1 (spec.md): GET /api/vaccine-types returns the seeded, active-only
/// catalog grouped/sorted by category then sortOrder (data-model.md seed list).</summary>
public class VaccineTypeListTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task ListVaccineTypes_ReturnsSeededCatalog_ActiveOnly_SortedByCategoryThenSortOrder()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/vaccine-types", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = (await response.Content.ReadFromJsonAsync<List<VaccineTypeResponse>>())!;

        Assert.Equal(9, list.Count);
        Assert.All(list, v => Assert.False(string.IsNullOrWhiteSpace(v.Name)));

        var categories = list.Select(v => v.Category).ToList();
        Assert.Equal(2, categories.Distinct().Count());
        Assert.Contains("basisvaccinatieschema", categories);
        Assert.Contains("aanbevolen_niet_gratis", categories);

        // Entries with the same category are grouped contiguously (FR-013) — no interleaving.
        // Collapsing consecutive duplicates ("runs") must equal the count of distinct categories;
        // if a category appeared in two separate blocks, this count would be higher.
        var runCount = categories.Zip(categories.Skip(1), (a, b) => a != b).Count(changed => changed) + 1;
        Assert.Equal(categories.Distinct().Count(), runCount);

        // Within each category group, sortOrder is ascending.
        foreach (var group in list.GroupBy(v => v.Category))
            Assert.Equal(group.Select(v => v.SortOrder).OrderBy(x => x), group.Select(v => v.SortOrder));

        Assert.Contains(list, v => v.Name == "HPV");
        Assert.Contains(list, v => v.Name == "MenB");
    }
}
