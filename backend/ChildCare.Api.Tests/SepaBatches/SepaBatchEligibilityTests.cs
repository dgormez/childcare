using System.Net;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.CodaTransactions.CodaTransactionsTestSupport;
using static ChildCare.Api.Tests.SepaBatches.SepaBatchesTestSupport;

namespace ChildCare.Api.Tests.SepaBatches;

/// <summary>Feature 026, tasks.md T012 — User Story 1's eligibility view (FR-001/FR-004).</summary>
public class SepaBatchEligibilityTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Eligibility_SplitsInvoicesByMandateState()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);

        // Child A: signed, non-revoked mandate -> eligible.
        var childA = await CreateChildAsync(client, org.AccessToken);
        var contractA = await CreateAndActivateContractAsync(client, org.AccessToken, childA.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contractA.Id, "BE71096123456769");
        var invoiceA = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, childA.Id, location.Id, 2027, 4);

        // Child B: never signed a mandate -> excluded, NoMandate.
        var childB = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, childB.Id, location.Id, new DateOnly(2027, 4, 1));
        var invoiceB = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, childB.Id, location.Id, 2027, 4);

        // Child C: signed then revoked -> excluded, MandateRevoked.
        var childC = await CreateChildAsync(client, org.AccessToken);
        var contractC = await CreateAndActivateContractAsync(client, org.AccessToken, childC.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contractC.Id, "BE62510007547061");
        var invoiceC = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, childC.Id, location.Id, 2027, 4);
        var revokeResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contractC.Id}/revoke-sepa-mandate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var eligibility = await GetEligibilityAsync(client, org.AccessToken, location.Id, 2027, 4);

        Assert.True(eligibility.CreditorConfigured);
        Assert.Single(eligibility.Eligible, e => e.InvoiceId == invoiceA.Id);
        Assert.Single(eligibility.Excluded, e => e.InvoiceId == invoiceB.Id && e.Reason == "NoMandate");
        Assert.Single(eligibility.Excluded, e => e.InvoiceId == invoiceC.Id && e.Reason == "MandateRevoked");
    }

    [Fact]
    public async Task Eligibility_MissingCreditorConfiguration_ReportsNotConfigured()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var eligibility = await GetEligibilityAsync(client, org.AccessToken, location.Id, 2027, 4);

        Assert.False(eligibility.CreditorConfigured);
    }

    // Convergence pass F2 — the two asymmetric configuration cases, distinct from "neither
    // configured" above.
    [Fact]
    public async Task Eligibility_OnlyCreditorIdentifierConfigured_StillReportsNotConfigured()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var orgResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/organisations/me", org.AccessToken, new UpdateOrganisationRequest(null, "BE68ZZZ0123456789")));
        Assert.Equal(HttpStatusCode.OK, orgResponse.StatusCode);

        var eligibility = await GetEligibilityAsync(client, org.AccessToken, location.Id, 2027, 4);

        Assert.False(eligibility.CreditorConfigured);
    }

    [Fact]
    public async Task Eligibility_OnlyBankAccountConfigured_StillReportsNotConfigured()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var locationResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/invoice-settings", org.AccessToken,
            new UpdateLocationInvoiceSettingsRequest(null, "BE68539007547034", 14)));
        Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode);

        var eligibility = await GetEligibilityAsync(client, org.AccessToken, location.Id, 2027, 4);

        Assert.False(eligibility.CreditorConfigured);
    }

    // Convergence pass F1 — the third documented exclusion reason (data-model.md's Eligibility
    // rule), never exercised until now.
    [Fact]
    public async Task Eligibility_NonPositiveAmountInvoice_IsExcludedWithReason()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sepa Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await ConfigureSepaCreditorAsync(client, org.AccessToken, location.Id, "BE68ZZZ0123456789", "BE68539007547034");
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id, new DateOnly(2027, 4, 1));
        await SeedFullSepaMandateAsync(factory.Services, schema, contract.Id, "BE71096123456769");
        var invoice = await CreateSentInvoiceAsync(client, org.AccessToken, factory.Services, schema, child.Id, location.Id, 2027, 4);

        // Zero-amount invoices shouldn't occur via normal billing (feature 014), but the
        // eligibility rule's defensive boundary (data-model.md) needs a real row to exercise it.
        using (var scope = factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(schema);
            var invoiceEntity = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
            invoiceEntity.TotalCents = 0;
            await db.SaveChangesAsync();
        }

        var eligibility = await GetEligibilityAsync(client, org.AccessToken, location.Id, 2027, 4);

        Assert.Single(eligibility.Excluded, e => e.InvoiceId == invoice.Id && e.Reason == "NonPositiveAmount");
    }
}
