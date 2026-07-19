using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>
/// Feature 030 User Story 2 (sibling discount) and User Story 3 (family invoice bundling) —
/// both computed in the same GenerateInvoicesCommand pass, grouped by each child's primary
/// contact (spec.md FR-004/FR-007/FR-008, research.md R2/R3/R4).
/// </summary>
public class SiblingDiscountAndBundlingTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private const int DailyRateCents = 3500;
    private const string SiblingDiscountLabel = "invoices.lineItems.siblingDiscount";

    private static async Task<ContractResponse> CreateContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, DateOnly startDate, DayOfWeek weekday)
    {
        var request = new CreateContractRequest(
            locationId, startDate, null,
            [new ContractedDayRequest(weekday, new TimeOnly(8, 0), new TimeOnly(17, 0))], DailyRateCents, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, request));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        return contract;
    }

    private static async Task SetSiblingBillingAsync(HttpClient client, string accessToken, Guid locationId, decimal discountPct, bool bundlingEnabled)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{locationId}/sibling-billing-settings", accessToken,
            new UpdateLocationSiblingBillingSettingsRequest(discountPct, bundlingEnabled)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> LinkSecondaryContactAsync(HttpClient client, string accessToken, Guid childId, Guid contactId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contacts", accessToken,
            new LinkContactToChildRequest(contactId, "EmergencyContact", false, false)));

    private static async Task<List<InvoiceResponse>> GenerateAsync(HttpClient client, string accessToken, Guid locationId, int year, int month)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/invoices/generate", accessToken, new GenerateInvoicesRequest(year, month)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
    }

    private static InvoiceExtraChargeResponse? SiblingDiscountLine(InvoiceResponse invoice) =>
        invoice.LineItems.ExtraCharges.SingleOrDefault(c => c.Label == SiblingDiscountLabel);

    [Fact]
    public async Task Generate_TwoSiblingsSharedPrimaryContact_DiscountsLaterEnrolledChildOnly()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await SetSiblingBillingAsync(client, org.AccessToken, location.Id, 10, false);

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var earlierChild = await CreateChildAsync(client, org.AccessToken, "Emma");
        var laterChild = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, earlierChild.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, laterChild.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, earlierChild.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, laterChild.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = earlierChild.Id, LocationId = location.Id, Date = new DateOnly(2027, 8, 2), Status = AttendanceStatus.Present });
        // laterChild's contract starts 2027-08-08 — an attendance date must be on/after that for
        // BillableDayCalculator.EffectiveRange to count it (a date before contract start falls
        // outside the effective billable range regardless of Status).
        db.AttendanceRecords.Add(new AttendanceRecord { ChildId = laterChild.Id, LocationId = location.Id, Date = new DateOnly(2027, 8, 10), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        var earlierInvoice = invoices.Single(i => i.ChildId == earlierChild.Id);
        var laterInvoice = invoices.Single(i => i.ChildId == laterChild.Id);
        Assert.Null(SiblingDiscountLine(earlierInvoice));
        var discount = SiblingDiscountLine(laterInvoice);
        Assert.NotNull(discount);
        Assert.Equal(-350, discount!.AmountCents); // 10% of one present day (3500)
        Assert.Equal(DailyRateCents - 350, laterInvoice.TotalCents);
        Assert.Equal(DailyRateCents, earlierInvoice.TotalCents);
    }

    [Fact]
    public async Task Generate_DiscountZero_NoDiscountLineForAnyChild()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        // Discount defaults to 0 — no settings call needed.

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child1.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.All(invoices, i => Assert.Null(SiblingDiscountLine(i)));
    }

    [Fact]
    public async Task Generate_SiblingsAtDifferentLocations_NoDiscountAtEither()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "B");
        await SetSiblingBillingAsync(client, org.AccessToken, locationA.Id, 10, false);
        await SetSiblingBillingAsync(client, org.AccessToken, locationB.Id, 10, false);

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var childAtA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childAtB = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, childAtA.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, childAtB.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, childAtA.Id, locationA.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, childAtB.Id, locationB.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var invoicesA = await GenerateAsync(client, org.AccessToken, locationA.Id, 2027, 8);
        var invoicesB = await GenerateAsync(client, org.AccessToken, locationB.Id, 2027, 8);

        Assert.Null(SiblingDiscountLine(invoicesA.Single()));
        Assert.Null(SiblingDiscountLine(invoicesB.Single()));
    }

    [Fact]
    public async Task Generate_NoSharedPrimaryContact_NoDiscountForEither()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await SetSiblingBillingAsync(client, org.AccessToken, location.Id, 10, false);

        var contactA = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Anna");
        var contactB = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Bram");
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child1.Id, contactA.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contactB.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.All(invoices, i => Assert.Null(SiblingDiscountLine(i)));
    }

    [Fact]
    public async Task Generate_ThreeSiblingsSharedPrimaryContact_DiscountsAllButEarliestEnrolled()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await SetSiblingBillingAsync(client, org.AccessToken, location.Id, 10, false);

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        var child3 = await CreateChildAsync(client, org.AccessToken, "Nora");
        await LinkContactAsync(client, org.AccessToken, child1.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, child3.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);
        await CreateContractAsync(client, org.AccessToken, child3.Id, location.Id, new DateOnly(2027, 8, 15), DayOfWeek.Wednesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.Null(SiblingDiscountLine(invoices.Single(i => i.ChildId == child1.Id)));
        Assert.NotNull(SiblingDiscountLine(invoices.Single(i => i.ChildId == child2.Id)));
        Assert.NotNull(SiblingDiscountLine(invoices.Single(i => i.ChildId == child3.Id)));
    }

    // Feature 030 Convergence (T068/T069) — spec.md Assumptions: when two siblings' contracts
    // share the exact same start date (e.g. twins), the earlier-*created* contract record is the
    // deterministic secondary tie-breaker for which child is full price.
    [Fact]
    public async Task Generate_TwoSiblingsSameContractStartDate_TieBreaksByEarlierCreatedContract()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await SetSiblingBillingAsync(client, org.AccessToken, location.Id, 10, false);

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var earlierCreatedChild = await CreateChildAsync(client, org.AccessToken, "Emma");
        var laterCreatedChild = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, earlierCreatedChild.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, laterCreatedChild.Id, contact.Id);

        // Same StartDate for both — only creation order can break the tie.
        var sameStartDate = new DateOnly(2027, 8, 1);
        await CreateContractAsync(client, org.AccessToken, earlierCreatedChild.Id, location.Id, sameStartDate, DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, laterCreatedChild.Id, location.Id, sameStartDate, DayOfWeek.Tuesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.Null(SiblingDiscountLine(invoices.Single(i => i.ChildId == earlierCreatedChild.Id)));
        Assert.NotNull(SiblingDiscountLine(invoices.Single(i => i.ChildId == laterCreatedChild.Id)));
    }

    [Fact]
    public async Task Generate_SiblingsWithDifferentPrimaryContacts_NeverGroupedEvenWithSharedSecondaryContact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Sibling Discount Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await SetSiblingBillingAsync(client, org.AccessToken, location.Id, 10, true);

        var primaryA = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Anna");
        var primaryB = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Bram");
        var sharedSecondary = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Chris");
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child1.Id, primaryA.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, primaryB.Id);
        await LinkSecondaryContactAsync(client, org.AccessToken, child1.Id, sharedSecondary.Id);
        await LinkSecondaryContactAsync(client, org.AccessToken, child2.Id, sharedSecondary.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.All(invoices, i => Assert.Null(SiblingDiscountLine(i)));
        Assert.All(invoices, i => Assert.Null(i.FamilyGroupId));
    }

    [Fact]
    public async Task Generate_BundlingDisabled_FamilyGroupIdStaysNull()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Family Bundling Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        // Bundling defaults to disabled — no settings call needed.

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child1.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        Assert.All(invoices, i => Assert.Null(i.FamilyGroupId));
    }

    [Fact]
    public async Task Generate_BundlingEnabled_TwoSiblingsShareNewFamilyGroupId()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Family Bundling Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await SetSiblingBillingAsync(client, org.AccessToken, location.Id, 0, true);

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        var child1 = await CreateChildAsync(client, org.AccessToken, "Emma");
        var child2 = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await LinkContactAsync(client, org.AccessToken, child1.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, child2.Id, contact.Id);
        await CreateContractAsync(client, org.AccessToken, child1.Id, location.Id, new DateOnly(2027, 8, 1), DayOfWeek.Monday);
        await CreateContractAsync(client, org.AccessToken, child2.Id, location.Id, new DateOnly(2027, 8, 8), DayOfWeek.Tuesday);

        var invoices = await GenerateAsync(client, org.AccessToken, location.Id, 2027, 8);

        var invoice1 = invoices.Single(i => i.ChildId == child1.Id);
        var invoice2 = invoices.Single(i => i.ChildId == child2.Id);
        Assert.NotNull(invoice1.FamilyGroupId);
        Assert.Equal(invoice1.FamilyGroupId, invoice2.FamilyGroupId);
    }
}
