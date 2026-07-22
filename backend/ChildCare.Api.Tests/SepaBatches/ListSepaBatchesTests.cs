using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;
using static ChildCare.Api.Tests.SepaBatches.SepaBatchesTestSupport;

namespace ChildCare.Api.Tests.SepaBatches;

/// <summary>Feature 026, tasks.md T018 — batch history (FR-008).</summary>
public class ListSepaBatchesTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task ListBatches_ScopedToLocation_NewestFirst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, locationA.Id, "BE68ZZZ0123456789", "BE68539007547034");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, locationB.Id, "BE68ZZZ0123456789", "BE62510007547061");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var childA = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, childA.Id);
        var contractA = await CreateAndActivateContractAsync(client, org.AccessToken, childA.Id, locationA.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contractA.Id, "BE71096123456769");
        var invoiceA = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, childA.Id, locationA.Id, 2027, 4);
        await GenerateBatchAsync(client, org.AccessToken, locationA.Id, [invoiceA.Id], NextBusinessDay());

        var childB = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, childB.Id);
        var contractB = await CreateAndActivateContractAsync(client, org.AccessToken, childB.Id, locationB.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contractB.Id, "BE68539007547034");
        var invoiceB = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, childB.Id, locationB.Id, 2027, 4);
        await GenerateBatchAsync(client, org.AccessToken, locationB.Id, [invoiceB.Id], NextBusinessDay());

        var responseA = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{locationA.Id}/sepa-batches", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        var batchesA = (await responseA.Content.ReadFromJsonAsync<List<SepaBatchResponse>>())!;
        Assert.Single(batchesA);
        Assert.Equal(invoiceA.TotalCents, batchesA[0].TotalCents);
    }
}
