using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task GetInvoices_ChildAtTwoLocations_ReturnsBothInvoices_AttributedToCorrectLocation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var firstLocation = await CreateLocationAsync(client, org.AccessToken, "First");
        var secondLocation = await CreateLocationAsync(client, org.AccessToken, "Second");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var invoiceAtFirst = await CreateDraftInvoiceAsync(client, org.AccessToken, firstLocation.Id, child.Id, 2027, 11, DayOfWeek.Monday);
        var invoiceAtSecond = await CreateDraftInvoiceAsync(client, org.AccessToken, secondLocation.Id, child.Id, 2027, 11, DayOfWeek.Tuesday);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoiceAtFirst.Id, invoiceAtSecond.Id])));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/invoices", parentToken));
        var invoices = (await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;

        Assert.Equal(2, invoices.Count);
        Assert.Contains(invoices, i => i.LocationId == firstLocation.Id && i.LocationName == "First");
        Assert.Contains(invoices, i => i.LocationId == secondLocation.Id && i.LocationName == "Second");
    }

    // Feature 030 (US3) — invoices sharing a FamilyGroupId collapse into one combined entry
    // (contracts/family-siblings-api.md); a non-grouped invoice keeps its normal single shape.
    [Fact]
    public async Task GetInvoices_FamilyGroup_CollapsesIntoOneCombinedEntry_UngroupedInvoiceStaysNormal()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/sibling-billing-settings", org.AccessToken,
            new UpdateLocationSiblingBillingSettingsRequest(0, true)));

        var (child1, contact, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        var ungroupedChild = await CreateChildAsync(client, org.AccessToken, "Nora");
        await LinkContactAsync(client, org.AccessToken, ungroupedChild.Id, contact.Id, relationship: "Father");
        // A third linked child at a location with bundling disabled never joins the group.
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");

        var invoice1 = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child1.Id, 2027, 10, DayOfWeek.Monday);
        var invoice2 = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child2.Id, 2027, 10, DayOfWeek.Tuesday);
        var ungroupedInvoice = await CreateDraftInvoiceAsync(client, org.AccessToken, otherLocation.Id, ungroupedChild.Id, 2027, 10, DayOfWeek.Wednesday);
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/invoices/send", org.AccessToken,
            new SendInvoicesRequest([invoice1.Id, invoice2.Id, ungroupedInvoice.Id])));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/invoices", parentToken));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(2, json.GetArrayLength());
        var familyEntry = json.EnumerateArray().Single(e => e.TryGetProperty("children", out _));
        var normalEntry = json.EnumerateArray().Single(e => !e.TryGetProperty("children", out _));

        Assert.Equal(2, familyEntry.GetProperty("children").GetArrayLength());
        Assert.Equal(invoice1.SubtotalCents + invoice2.SubtotalCents, familyEntry.GetProperty("totalCents").GetInt32());
        Assert.Equal(ungroupedChild.Id.ToString(), normalEntry.GetProperty("childId").GetString());

        // Feature 030 Convergence (T070) — each child line must expose its own underlying
        // InvoiceId so the client can target the online payment-link endpoint for the bundle.
        var childLineInvoiceIds = familyEntry.GetProperty("children").EnumerateArray()
            .Select(c => c.GetProperty("invoiceId").GetString())
            .ToList();
        Assert.Contains(invoice1.Id.ToString(), childLineInvoiceIds);
        Assert.Contains(invoice2.Id.ToString(), childLineInvoiceIds);
    }

    // Feature 030 (US5, research.md R8) — a deactivated child's invoices must remain reachable
    // read-only for the parent (no new authorization gap introduced by 030).
    [Fact]
    public async Task GetInvoices_DeactivatedChild_StillSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Parent Invoices Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        var invoice = await CreateDraftInvoiceAsync(client, org.AccessToken, location.Id, child.Id, 2027, 9);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));
        // A child can't be deactivated while an active contract exists (ContractChildDeactivationGuard) — terminate it first.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{invoice.ContractId}/terminate", org.AccessToken, new TerminateContractRequest(new DateOnly(2027, 9, 30))));
        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/invoices", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoices = (await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        Assert.Contains(invoices, i => i.ChildId == child.Id);
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
