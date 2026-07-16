using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.FiscalAttestations.FiscalAttestationTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.FiscalAttestations;

/// <summary>Feature 015 — spec.md User Story 1 (director bulk-generates). FR-001/FR-003/FR-005/
/// FR-009/FR-010/FR-016.</summary>
public class GenerateFiscalAttestationsCommandTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Generate_MultipleChildren_ProducesOnePerEligibleChild_SkipsZeroPaidInvoices_SplitsMultiLocationChild()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Generate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var firstLocation = await CreateLocationAsync(client, org.AccessToken, "First");
        var secondLocation = await CreateLocationAsync(client, org.AccessToken, "Second");

        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, firstLocation.Id, childA.Id, 2027, 1);

        var childB = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await CreateAndActivateContractAsync(client, org.AccessToken, firstLocation.Id, childB.Id, new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31), 3500);
        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, firstLocation.Id, childB.Id, 2027, 1);
        await CreateAndActivateContractAsync(client, org.AccessToken, secondLocation.Id, childB.Id, new DateOnly(2027, 4, 1), null, 4000, DayOfWeek.Tuesday);
        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, secondLocation.Id, childB.Id, 2027, 4);

        var childC = await CreateChildAsync(client, org.AccessToken, "NoPaidInvoices");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var result = (await response.Content.ReadFromJsonAsync<GenerateFiscalAttestationsResponse>())!;

        // childA: 1 pair. childB: 2 pairs (two locations). childC: 0 pairs (excluded, not a failure).
        Assert.Equal(3, result.Results.Count);
        Assert.All(result.Results, r => Assert.Equal("generated", r.Status));
        Assert.Contains(result.Results, r => r.ChildId == childA.Id && r.LocationId == firstLocation.Id);
        Assert.Contains(result.Results, r => r.ChildId == childB.Id && r.LocationId == firstLocation.Id);
        Assert.Contains(result.Results, r => r.ChildId == childB.Id && r.LocationId == secondLocation.Id);
        Assert.DoesNotContain(result.Results, r => r.ChildId == childC.Id);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        Assert.Equal(3, list.Count);
        Assert.All(list, a => Assert.Equal("generated", a.Status));
    }

    [Fact]
    public async Task Generate_ReRunForYearWithExistingAttestations_LeavesExistingUntouched_OnlyGeneratesNewChildren()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Generate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, childA.Id, 2027, 1);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var firstListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var firstList = (await firstListResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        var originalGeneratedAt = firstList.Single().GeneratedAt;

        // A new invoice appears for childA after the first run — bulk re-run must NOT touch the
        // already-generated attestation (FR-009); only regenerate does that (see RegenerateTests).
        var childB = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, childB.Id, 2027, 2);

        var secondResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var secondResult = (await secondResponse.Content.ReadFromJsonAsync<GenerateFiscalAttestationsResponse>())!;

        Assert.Contains(secondResult.Results, r => r.ChildId == childA.Id && r.Status == "alreadyExists");
        Assert.Contains(secondResult.Results, r => r.ChildId == childB.Id && r.Status == "generated");

        var secondListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var secondList = (await secondListResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        // Millisecond-tolerant: a DateTime round-tripped through PostgreSQL's timestamptz twice
        // can differ by a few ticks (same precision class as RegenerateInvoiceTests, 014).
        Assert.True(Math.Abs((originalGeneratedAt!.Value - secondList.Single(a => a.ChildId == childA.Id).GeneratedAt!.Value).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task Generate_NotifiesEveryLinkedContact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Generate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, contact, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var contactRow = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
        contactRow.PushToken = "ExponentPushToken[fiscal-attestation-test]";
        await db.SaveChangesAsync();

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));

        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[fiscal-attestation-test]");

        var notification = await db.Notifications.SingleAsync(
            n => n.TenantUserId == contactRow.TenantUserId!.Value && n.Type == Domain.Enums.NotificationType.FiscalAttestationGenerated);
        Assert.Equal("parent.notifications.fiscal_attestation_ready.title", notification.TitleKey);
    }

    [Fact]
    public async Task Generate_OneChildFails_RestOfBatchStillCompletes()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Generate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var goodChild = await CreateChildAsync(client, org.AccessToken, "Good");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, goodChild.Id, 2027, 1);

        var brokenChild = await CreateChildAsync(client, org.AccessToken, "Broken");
        var brokenInvoice = await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, brokenChild.Id, 2027, 1);

        // Fault injection: corrupt the paid invoice's LineItems (valid JSON, wrong shape — an
        // array instead of the expected object) so aggregation throws during JSON
        // deserialization — proves per-item failure isolation actually works, rather than
        // asserting a try/catch shape exists without ever exercising the catch branch.
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var record = await db.Invoices.FirstAsync(i => i.Id == brokenInvoice.Id);
        record.LineItems = "[]";
        await db.SaveChangesAsync();

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var result = (await response.Content.ReadFromJsonAsync<GenerateFiscalAttestationsResponse>())!;

        Assert.Contains(result.Results, r => r.ChildId == goodChild.Id && r.Status == "generated");
        Assert.Contains(result.Results, r => r.ChildId == brokenChild.Id && r.Status == "failed");

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        Assert.Contains(list, a => a.ChildId == goodChild.Id && a.Status == "generated");
    }

    [Fact]
    public async Task Generate_AfterARegenerate_DoesNotClobberTheRegeneratedVersion()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Generate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));

        // Director corrects the attestation (quickstart.md Scenario 4) — an additional paid
        // month against the same already-active, open-ended contract.
        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);
        var regenerateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/fiscal-attestations/{child.Id}/{location.Id}/2027/regenerate", org.AccessToken));
        var corrected = (await regenerateResponse.Content.ReadFromJsonAsync<FiscalAttestationResponse>())!;

        // A routine bulk re-run must not reset the deliberate correction.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var afterBulkRerun = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        Assert.Equal(corrected.TotalAmountCents, afterBulkRerun.TotalAmountCents);
        // Millisecond-tolerant: a DateTime round-tripped through PostgreSQL's timestamptz twice
        // can differ by a few ticks (same precision class as RegenerateInvoiceTests, 014) — this
        // is the exact assertion that flaked in CI, confirming the class of bug.
        Assert.True(Math.Abs((corrected.GeneratedAt!.Value - afterBulkRerun.GeneratedAt!.Value).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task Generate_ChildLeftMidYear_AttestationCoversOnlyEnrolledPaidMonths()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Generate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        // Contract ends 2027-03-31 — the child departed after March.
        await CreateAndActivateContractAsync(client, org.AccessToken, location.Id, child.Id, new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31), 3500);
        var jan = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);
        var feb = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);
        var mar = await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 3);

        // No contract covers April onward, so no invoice — and therefore nothing paid — can
        // exist for the months after departure.
        var aprilGenerateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 4)));
        var aprilInvoices = (await aprilGenerateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!;
        Assert.DoesNotContain(aprilInvoices, i => i.ChildId == child.Id);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var attestation = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        var period = Assert.Single(attestation.Periods!);
        Assert.Equal(new DateOnly(2027, 1, 1), period.PeriodStart);
        Assert.Equal(new DateOnly(2027, 3, 31), period.PeriodEnd);
        Assert.Equal(jan.TotalCents + feb.TotalCents + mar.TotalCents, attestation.TotalAmountCents);
    }
}
