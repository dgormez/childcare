using System.Net;
using System.Net.Http.Json;
using System.Text;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.DevelopmentalMilestones;

/// <summary>User Story 4 (spec.md): on-demand milestone portfolio PDF export.</summary>
public class MilestonePortfolioPdfTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static void AssertValidPdf(byte[] bytes)
    {
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task DirectorPdf_ForChildWithObservations_IsAValidPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/milestone-portfolio/pdf", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        AssertValidPdf(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task DirectorPdf_ForChildWithNoObservations_StillSucceeds_ShowingEmptyState()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/milestone-portfolio/pdf", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertValidPdf(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ParentPdf_ForLinkedChild_IsAValidPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{child.Id}/milestone-portfolio/pdf", parentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertValidPdf(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ParentPdf_ForChildNotLinkedToCaller_IsForbidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/children/{otherChild.Id}/milestone-portfolio/pdf", parentToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
