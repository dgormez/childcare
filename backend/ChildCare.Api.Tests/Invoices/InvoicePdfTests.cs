using System.Net;
using System.Net.Http.Json;
using System.Text;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>Feature 014 — spec.md FR-005 (PDF content), rendered on-demand (research.md R1).</summary>
public class InvoicePdfTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetPdf_ForSentInvoice_ReturnsValidPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice PDF Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/invoice-settings", org.AccessToken,
            new UpdateLocationInvoiceSettingsRequest("KDV-999", "BE68539007547034", 14)));
        await client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/organisations/me", org.AccessToken, new UpdateOrganisationRequest("0123.456.789", null)));

        var child = await CreateChildAsync(client, org.AccessToken);
        var contractRequest = new CreateContractRequest(
            location.Id, new DateOnly(2027, 9, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var contractResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken, contractRequest));
        var contract = (await contractResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));

        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 9)));
        var invoice = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single();
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        var pdfResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}/pdf", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task GetPdf_ForNonExistentInvoice_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice PDF Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{Guid.NewGuid()}/pdf", org.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.invoice.not_found", await response.Content.ReadAsStringAsync());
    }
}
