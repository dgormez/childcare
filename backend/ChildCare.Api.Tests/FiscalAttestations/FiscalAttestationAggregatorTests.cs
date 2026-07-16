using ChildCare.Application.FiscalAttestations;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.FiscalAttestations.FiscalAttestationTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.FiscalAttestations;

/// <summary>Feature 015 — spec.md FR-002/FR-004/FR-005, research.md R3/R6.</summary>
public class FiscalAttestationAggregatorTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Aggregate_SameRateAcrossMonths_MergesIntoOnePeriod()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Aggregator Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, location.Id, child.Id, new DateOnly(2027, 1, 1), null, 3500);

        var jan = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);
        var feb = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var aggregator = new FiscalAttestationAggregator(db);

        var result = await aggregator.AggregateAsync(child.Id, location.Id, 2027);

        var period = Assert.Single(result.Periods);
        Assert.Equal(new DateOnly(2027, 1, 1), period.PeriodStart);
        Assert.Equal(new DateOnly(2027, 2, 28), period.PeriodEnd);
        Assert.Equal(3500, period.DailyRateCents);
        Assert.Equal(jan.TotalCents + feb.TotalCents, period.AmountCents);
        Assert.Equal(jan.TotalCents + feb.TotalCents, result.TotalAmountCents);
    }

    [Fact]
    public async Task Aggregate_MidYearRateChange_SplitsIntoTwoPeriods()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Aggregator Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contract1 = await CreateAndActivateContractAsync(client, org.AccessToken, location.Id, child.Id, new DateOnly(2027, 1, 1), null, 3500);
        var jan = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);

        await AmendContractRateAsync(client, org.AccessToken, contract1.Id, location.Id, new DateOnly(2027, 7, 1), 4000);
        var jul = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 7);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var aggregator = new FiscalAttestationAggregator(db);

        var result = await aggregator.AggregateAsync(child.Id, location.Id, 2027);

        Assert.Equal(2, result.Periods.Count);
        Assert.Equal(3500, result.Periods[0].DailyRateCents);
        Assert.Equal(4000, result.Periods[1].DailyRateCents);
        Assert.Equal(jan.TotalCents, result.Periods[0].AmountCents);
        Assert.Equal(jul.TotalCents, result.Periods[1].AmountCents);
        Assert.Equal(jan.TotalCents + jul.TotalCents, result.TotalAmountCents);
    }

    [Fact]
    public async Task Aggregate_MoreThanFourRateChanges_ConsolidatesOldestOverflowIntoFirstPeriod()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Aggregator Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateContractAsync(client, org.AccessToken, location.Id, child.Id, new DateOnly(2027, 1, 1), null, 3000);
        var jan = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);

        contract = await AmendContractRateAsync(client, org.AccessToken, contract.Id, location.Id, new DateOnly(2027, 2, 1), 3200);
        var feb = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);

        contract = await AmendContractRateAsync(client, org.AccessToken, contract.Id, location.Id, new DateOnly(2027, 3, 1), 3400);
        var mar = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 3);

        contract = await AmendContractRateAsync(client, org.AccessToken, contract.Id, location.Id, new DateOnly(2027, 4, 1), 3600);
        var apr = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 4);

        contract = await AmendContractRateAsync(client, org.AccessToken, contract.Id, location.Id, new DateOnly(2027, 5, 1), 3800);
        var may = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 5);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var aggregator = new FiscalAttestationAggregator(db);

        var result = await aggregator.AggregateAsync(child.Id, location.Id, 2027);

        // 5 raw monthly periods consolidate to 4: Jan+Feb merge into the first retained period.
        Assert.Equal(4, result.Periods.Count);
        Assert.Null(result.Periods[0].DailyRateCents);
        Assert.Equal(new DateOnly(2027, 1, 1), result.Periods[0].PeriodStart);
        Assert.Equal(new DateOnly(2027, 2, 28), result.Periods[0].PeriodEnd);
        Assert.Equal(jan.TotalCents + feb.TotalCents, result.Periods[0].AmountCents);
        Assert.Equal(3400, result.Periods[1].DailyRateCents);
        Assert.Equal(3600, result.Periods[2].DailyRateCents);
        Assert.Equal(3800, result.Periods[3].DailyRateCents);
        // Totals never approximate even though the breakdown is coarser (spec.md Edge Cases).
        Assert.Equal(jan.TotalCents + feb.TotalCents + mar.TotalCents + apr.TotalCents + may.TotalCents, result.TotalAmountCents);
    }

    [Fact]
    public async Task Aggregate_NoPaidInvoices_ReturnsNoPeriodsAndZeroTotal()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Aggregator Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var aggregator = new FiscalAttestationAggregator(db);

        var result = await aggregator.AggregateAsync(child.Id, location.Id, 2027);

        Assert.Empty(result.Periods);
        Assert.Equal(0, result.TotalAmountCents);
    }

    [Fact]
    public async Task Aggregate_ChildAtTwoLocations_EachLocationOnlySeesItsOwnInvoices()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Aggregator Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var firstLocation = await CreateLocationAsync(client, org.AccessToken, "First");
        var secondLocation = await CreateLocationAsync(client, org.AccessToken, "Second");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, firstLocation.Id, child.Id, new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31), 3500);
        var jan = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, firstLocation.Id, child.Id, 2027, 1);
        await CreateAndActivateContractAsync(client, org.AccessToken, secondLocation.Id, child.Id, new DateOnly(2027, 4, 1), null, 4200, DayOfWeek.Tuesday);
        var apr = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, secondLocation.Id, child.Id, 2027, 4);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var aggregator = new FiscalAttestationAggregator(db);

        var firstResult = await aggregator.AggregateAsync(child.Id, firstLocation.Id, 2027);
        var secondResult = await aggregator.AggregateAsync(child.Id, secondLocation.Id, 2027);

        Assert.Equal(jan.TotalCents, firstResult.TotalAmountCents);
        Assert.Equal(apr.TotalCents, secondResult.TotalAmountCents);
    }
}
