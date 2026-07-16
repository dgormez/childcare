using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.FiscalAttestations.FiscalAttestationTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.FiscalAttestations;

/// <summary>Feature 015 — spec.md User Story 2 (parent views/downloads). FR-011.</summary>
public class GetParentFiscalAttestationsQueryTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetAttestations_ReturnsOnlyLinkedChildrens_AndAnyLinkedContactSeesSame()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Parent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child1.Id, 2027, 1);
        var child2 = await CreateChildAsync(client, org.AccessToken, "OtherLinked");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child2.Id, 2027, 1, weekday: DayOfWeek.Tuesday);
        var unlinkedChild = await CreateChildAsync(client, org.AccessToken, "Unlinked");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, unlinkedChild.Id, 2027, 1, weekday: DayOfWeek.Wednesday);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/fiscal-attestations", parentToken));
        var attestations = (await response.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;

        Assert.Equal(2, attestations.Count);
        Assert.Contains(attestations, a => a.ChildId == child1.Id);
        Assert.Contains(attestations, a => a.ChildId == child2.Id);
        Assert.DoesNotContain(attestations, a => a.ChildId == unlinkedChild.Id);
    }

    [Fact]
    public async Task GetAttestations_NoneGeneratedYet_ReturnsEmptyList_NotError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Parent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/fiscal-attestations", parentToken));
        var attestations = (await response.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;

        Assert.Empty(attestations);
    }
}
