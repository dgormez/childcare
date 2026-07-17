using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.DevelopmentalMilestones;

/// <summary>User Story 3 (spec.md): parent views their child's shared milestone portfolio.</summary>
public class GetParentMilestonePortfolioQueryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetPortfolio_AsLinkedParent_ReturnsDomainGroupedStructure_WithoutHistory()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var domainsResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/developmental-domains", org.AccessToken));
        var domains = (await domainsResponse.Content.ReadFromJsonAsync<List<DevelopmentalDomainResponse>>())!;
        var milestoneId = domains.First().Milestones.First().Id;

        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, caregiver.Id, "1234");
        await client.SendAsync(DeviceRequest(HttpMethod.Post, $"/api/children/{child.Id}/milestone-observations", deviceToken, new
        {
            milestoneId,
            status = "emerging",
            observedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            notes = (string?)null,
        }));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/milestone-portfolio", parentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var portfolio = (await response.Content.ReadFromJsonAsync<MilestonePortfolioResponse>())!;

        Assert.Equal(7, portfolio.Domains.Count);
        var milestone = portfolio.Domains.SelectMany(d => d.Milestones).Single(m => m.Id == milestoneId);
        Assert.Equal("emerging", milestone.CurrentStatus);
        Assert.Null(milestone.History); // parents don't see per-observation history
    }

    [Fact]
    public async Task GetPortfolio_ForChildWithNoObservations_ReturnsSuccessfully_NotAnError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/milestone-portfolio", parentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var portfolio = (await response.Content.ReadFromJsonAsync<MilestonePortfolioResponse>())!;
        Assert.All(portfolio.Domains.SelectMany(d => d.Milestones), m => Assert.Null(m.CurrentStatus));
    }

    [Fact]
    public async Task GetPortfolio_ForChildNotLinkedToCaller_IsForbidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        // A second, unrelated child the above parent has no ChildContact link to.
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{otherChild.Id}/milestone-portfolio", parentToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken, string firstName) =>
        (await (await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;
}
