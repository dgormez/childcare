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
/// Feature 014 — spec.md User Story 3 (parent views and downloads their invoices). FR-008
/// (draft never visible), FR-010 (one entry per child), Security considerations (404, not 403,
/// for both "doesn't exist" and "not yours"/"draft" — indistinguishable).
/// </summary>
public class GetParentInvoicesTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<InvoiceResponse> CreateDraftInvoiceAsync(
        HttpClient client, string accessToken, Guid locationId, Guid childId, int year, int month, DayOfWeek weekday = DayOfWeek.Monday)
    {
        var request = new CreateContractRequest(
            locationId, new DateOnly(year, month, 1), null,
            [new ContractedDayRequest(weekday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        var invoices = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        return invoices.Single(i => i.ChildId == childId);
    }

    [Fact]
    public async Task GetInvoices_ReturnsOnlySentInvoices_NeverDraft()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var draftInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var beforeSendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/invoices", parentToken));
        var beforeSend = (await beforeSendResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        Assert.Empty(beforeSend);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([draftInvoice.Id])));

        var afterSendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/invoices", parentToken));
        var afterSend = (await afterSendResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        var entry = Assert.Single(afterSend);
        Assert.Equal("sent", entry.Status);
        Assert.Equal(child.Id, entry.ChildId);
    }

    [Fact]
    public async Task GetInvoices_MultipleChildren_ReturnsAllOfThem_AndSecondContactSeesSame()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);

        var invoice1 = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child1.Id, 2027, 10, DayOfWeek.Monday);
        var invoice2 = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child2.Id, 2027, 10, DayOfWeek.Tuesday);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice1.Id, invoice2.Id])));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/invoices", parentToken));
        var invoices = (await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;

        Assert.Equal(2, invoices.Count);
        Assert.Contains(invoices, i => i.ChildId == child1.Id);
        Assert.Contains(invoices, i => i.ChildId == child2.Id);
    }

    [Fact]
    public async Task GetPdf_ForInvoiceNotBelongingToParent_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");
        var otherInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, otherChild.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([otherInvoice.Id])));
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{otherInvoice.Id}/pdf", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPdf_ForOwnDraftInvoice_Returns404_SameAsNotBelongingToParent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var draftInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{draftInvoice.Id}/pdf", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPdf_ForOwnSentInvoice_ReturnsPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/invoices/{invoice.Id}/pdf", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }
}
