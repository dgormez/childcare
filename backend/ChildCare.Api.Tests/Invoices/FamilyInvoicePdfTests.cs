using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>
/// Feature 030 User Story 3 — the combined family invoice PDF (spec.md FR-008, research.md R5).
/// Same indistinguishable-not-found authorization pattern as the existing per-invoice PDF route
/// (GetParentInvoicesTests precedent).
/// </summary>
public class FamilyInvoicePdfTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<ContractResponse> CreateContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, DateOnly startDate, DayOfWeek weekday)
    {
        var request = new CreateContractRequest(
            locationId, startDate, null,
            [new ContractedDayRequest(weekday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        return contract;
    }

    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location, Guid FamilyGroupId, string ParentToken)> SetupBundledFamilyAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Family PDF Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/sibling-billing-settings", org.AccessToken,
            new UpdateLocationSiblingBillingSettingsRequest(0, true)));

        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 8)));
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        var familyGroupId = invoices.Select(i => i.FamilyGroupId).Distinct().Single()!.Value;

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/invoices/send", org.AccessToken,
            new SendInvoicesRequest(invoices.Select(i => i.Id).ToList())));

        return (client, org, location, familyGroupId, parentToken);
    }

    [Fact]
    public async Task GetFamilyPdf_ForValidGroup_ReturnsOnePdf()
    {
        var (client, _, _, familyGroupId, parentToken) = await SetupBundledFamilyAsync();

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/family/{familyGroupId}/pdf", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task GetFamilyPdf_ForUnrelatedCaller_Returns404_IndistinguishableFromNotFound()
    {
        var (client, org, _, familyGroupId, _) = await SetupBundledFamilyAsync();
        var (_, _, otherParentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/family/{familyGroupId}/pdf", otherParentToken));
        var notFoundResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/family/{Guid.NewGuid()}/pdf", otherParentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
        Assert.Equal(await notFoundResponse.Content.ReadAsStringAsync(), await response.Content.ReadAsStringAsync());
    }
}
