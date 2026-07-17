using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.DevelopmentalMilestones;

/// <summary>Foundational: the seeded catalog (research.md R7) is present and complete.</summary>
public class DevelopmentalMilestoneCatalogSeedTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly string[] ExpectedDomainCodes =
        ["motor_gross", "motor_fine", "language", "cognitive", "social", "emotional", "self_care"];

    [Fact]
    public async Task ListDevelopmentalDomains_ContainsAllSevenDomains_WithMilestonesAcross0To36Months()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/developmental-domains", org.AccessToken));
        var domains = (await response.Content.ReadFromJsonAsync<List<DevelopmentalDomainResponse>>())!;

        Assert.Equal(7, domains.Count);
        foreach (var code in ExpectedDomainCodes)
            Assert.Contains(domains, d => d.Code == code);

        foreach (var domain in domains)
        {
            Assert.NotEmpty(domain.Milestones);
            Assert.All(domain.Milestones, m =>
            {
                Assert.False(string.IsNullOrWhiteSpace(m.DescriptionNl));
                Assert.False(string.IsNullOrWhiteSpace(m.DescriptionFr));
                Assert.False(string.IsNullOrWhiteSpace(m.DescriptionEn));
            });
            Assert.Contains(domain.Milestones, m => m.AgeFromMonths <= 3);
            Assert.Contains(domain.Milestones, m => m.AgeToMonths >= 24);
        }
    }
}
