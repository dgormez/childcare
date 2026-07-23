using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.DevelopmentalMilestones;

/// <summary>User Story 2 (spec.md): director views a child's milestone portfolio.</summary>
public class GetChildMilestonePortfolioQueryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetPortfolio_GroupsByDomain_AndFlagsAgeAppropriateBand()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, caregiver.Id, "1234");

        // An 18-month-old child so at least one seeded milestone (13-18 or 19-24 band) is
        // flagged current-focus regardless of exact seed content.
        var childResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken,
            new CreateChildRequest("Emma", "Peeters", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-18)), null, null, null, null, null, null, null, null, null, null)));
        var child = (await childResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        var domainsResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/developmental-domains", org.AccessToken));
        var domains = (await domainsResponse.Content.ReadFromJsonAsync<List<DevelopmentalDomainResponse>>())!;
        var milestoneId = domains.First().Milestones.First().Id;

        var record = await client.SendAsync(DeviceRequest(HttpMethod.Post, $"/api/children/{child.Id}/milestone-observations", deviceToken, new
        {
            milestoneId,
            status = "achieved",
            observedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            notes = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.Created, record.StatusCode);

        var portfolioResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/milestone-portfolio", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, portfolioResponse.StatusCode);
        var portfolio = (await portfolioResponse.Content.ReadFromJsonAsync<MilestonePortfolioResponse>())!;

        Assert.Equal(7, portfolio.Domains.Count);
        var observedMilestone = portfolio.Domains.SelectMany(d => d.Milestones).Single(m => m.Id == milestoneId);
        Assert.Equal("achieved", observedMilestone.CurrentStatus);
        Assert.NotNull(observedMilestone.History);
        Assert.Contains(portfolio.Domains.SelectMany(d => d.Milestones), m => m.IsCurrentFocus);
    }

    [Fact]
    public async Task GetPortfolio_ForChildWithNoObservations_ReturnsFullCatalog_WithNullStatuses()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/milestone-portfolio", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var portfolio = (await response.Content.ReadFromJsonAsync<MilestonePortfolioResponse>())!;

        Assert.Equal(7, portfolio.Domains.Count);
        Assert.All(portfolio.Domains.SelectMany(d => d.Milestones), m => Assert.Null(m.CurrentStatus));
    }
}
