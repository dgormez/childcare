using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using System.Xml.Schema;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;
using static ChildCare.Api.Tests.SepaBatches.SepaBatchesTestSupport;

namespace ChildCare.Api.Tests.SepaBatches;

/// <summary>Feature 026, tasks.md T013-T017/T016a/T016b — User Story 1's batch generation
/// (FR-002/FR-002a/FR-003/FR-004/FR-005/FR-006/FR-006a/FR-007/FR-013).</summary>
public class GenerateSepaBatchTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Generate_HappyPath_ProducesSchemaValidXml_AndClaimsInvoices()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 5, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 5);

        var executionDate = NextBusinessDay();
        var response = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], executionDate);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var xmlBytes = await response.Content.ReadAsByteArrayAsync();
        var xDocument = XDocument.Load(new MemoryStream(xmlBytes));
        var schemaSet = new XmlSchemaSet();
        var xsdPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChildCare.Infrastructure", "Sepa", "Schemas", "pain.008.001.02.xsd");
        using (var reader = System.Xml.XmlReader.Create(xsdPath))
            schemaSet.Add("urn:iso:std:iso:20022:tech:xsd:pain.008.001.02", reader);
        xDocument.Validate(schemaSet, null);

        var xml = System.Text.Encoding.UTF8.GetString(xmlBytes);
        Assert.Contains("<SeqTp>FRST</SeqTp>", xml);
        Assert.Contains(invoice.OgmReference, xml);

        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var updatedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("pendingdebit", updatedInvoice.Status);
        Assert.NotNull(updatedInvoice.SepaBatchId);

        var batches = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}/sepa-batches", org.AccessToken));
        var batchList = (await batches.Content.ReadFromJsonAsync<List<SepaBatchResponse>>())!;
        Assert.Single(batchList, b => b.InvoiceCount == 1 && b.TotalCents == invoice.TotalCents && b.ExecutionDate == executionDate);
    }

    [Fact]
    public async Task Generate_SecondBatchForSameMandate_UsesRcur()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");

        var invoice1 = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);
        var firstBatch = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice1.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, firstBatch.StatusCode);

        var invoice2 = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 5);
        var secondBatch = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice2.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, secondBatch.StatusCode);

        var xml = System.Text.Encoding.UTF8.GetString(await secondBatch.Content.ReadAsByteArrayAsync());
        Assert.Contains("<SeqTp>RCUR</SeqTp>", xml);
    }

    [Fact]
    public async Task Generate_MissingCreditorConfiguration_Returns422_NoStatusChange()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        var response = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var unchangedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("sent", unchangedInvoice.Status);
    }

    [Fact]
    public async Task Generate_ExecutionDateTooSoon_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], today);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Generate_InvoiceAlreadyPendingDebit_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        var executionDate = NextBusinessDay();
        var firstAttempt = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], executionDate);
        Assert.Equal(HttpStatusCode.OK, firstAttempt.StatusCode);

        var secondAttempt = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], executionDate);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task Generate_ConcurrentRequestsForSameInvoice_ClaimsItAtMostOnce()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        var executionDate = NextBusinessDay();
        var client2 = factory.CreateClient();
        var attempt1 = GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], executionDate);
        var attempt2 = GenerateBatchAsync(client2, org.AccessToken, location.Id, [invoice.Id], executionDate);
        var results = await Task.WhenAll(attempt1, attempt2);

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        var batches = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}/sepa-batches", org.AccessToken));
        var batchList = (await batches.Content.ReadFromJsonAsync<List<SepaBatchResponse>>())!;
        Assert.Single(batchList);
        Assert.Equal(1, batchList[0].InvoiceCount);
    }

    [Fact]
    public async Task Generate_UndecryptableIban_FailsWholeBatch_NoStatusChange()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        // Corrupt the ciphertext directly — simulates a key-rotation mismatch/corrupted value.
        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schema);
            var contractEntity = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
            contractEntity.SepaIbanEncrypted = "not-a-valid-protected-payload";
            await db.SaveChangesAsync();
        }

        var response = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var getInvoiceResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var unchangedInvoice = (await getInvoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        Assert.Equal("sent", unchangedInvoice.Status);

        var batches = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}/sepa-batches", org.AccessToken));
        var batchList = (await batches.Content.ReadFromJsonAsync<List<SepaBatchResponse>>())!;
        Assert.Empty(batchList);
    }

    [Fact]
    public async Task Generate_NoInvoicesSelected_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");

        var response = await GenerateBatchAsync(client, org.AccessToken, location.Id, [], NextBusinessDay());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // FR-010 (US3) precedent: a returned invoice is eligible for a later batch exactly like any
    // other Sent invoice — no special "previously returned" exclusion.
    [Fact]
    public async Task Generate_ReturnedInvoice_IsEligibleForALaterBatch()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        var firstBatch = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, firstBatch.StatusCode);

        var returnResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/invoices/{invoice.Id}/mark-sepa-returned", org.AccessToken,
            new MarkInvoiceSepaReturnedRequest("Insufficient funds")));
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);

        var secondBatch = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, secondBatch.StatusCode);
        var xml = System.Text.Encoding.UTF8.GetString(await secondBatch.Content.ReadAsByteArrayAsync());
        // Same mandate reference was already used in the first (later-returned) batch, so this
        // remains RCUR — a return doesn't reset the sequence type (research.md R3/CHK008).
        Assert.Contains("<SeqTp>RCUR</SeqTp>", xml);
    }

    // Feature 026, tasks.md T043 — integration-level companion to SepaSequenceTypeResolverTests'
    // unit test: a revoke-and-resign cycle (new SepaMandateReference) resets to FRST.
    [Fact]
    public async Task Generate_AfterRevokeAndResign_ResetsToFrst()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        var child = await CreateChildAsync(client, org.AccessToken);
        await SeedPrimaryContactAsync(factory.Services, schema, child.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");

        var invoice1 = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);
        var firstBatch = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice1.Id], NextBusinessDay());
        Assert.Equal(HttpStatusCode.OK, firstBatch.StatusCode);
        Assert.Contains("<SeqTp>FRST</SeqTp>", System.Text.Encoding.UTF8.GetString(await firstBatch.Content.ReadAsByteArrayAsync()));

        var revokeResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/revoke-sepa-mandate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        // Re-sign under a brand new mandate reference (bypassing the full public e-signature
        // ceremony, same as every other test here) — SeedFullSepaMandateAsync clears revocation
        // implicitly by overwriting SepaAuthorisedAt/SepaMandateReference with fresh values.
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schema);
            var contractEntity = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
            contractEntity.SepaRevokedAt = null;
            await db.SaveChangesAsync();
        }

        var invoice2 = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 5);
        var secondBatch = await GenerateBatchAsync(client, org.AccessToken, location.Id, [invoice2.Id], NextBusinessDay());

        Assert.Equal(HttpStatusCode.OK, secondBatch.StatusCode);
        Assert.Contains("<SeqTp>FRST</SeqTp>", System.Text.Encoding.UTF8.GetString(await secondBatch.Content.ReadAsByteArrayAsync()));
    }
}
