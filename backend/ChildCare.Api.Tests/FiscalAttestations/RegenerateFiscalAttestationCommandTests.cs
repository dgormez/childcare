using System.Net;
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

/// <summary>Feature 015 — spec.md User Story 3 (director corrects a single attestation).
/// FR-003/FR-008/FR-016.</summary>
public class RegenerateFiscalAttestationCommandTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Regenerate_ReAggregatesAndReplacesInPlace_SameIdUpdatedTotals()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var firstListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var original = (await firstListResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        // A late correction: an additional paid invoice for the same child/location/year,
        // against the same already-active, open-ended contract.
        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);

        var regenerateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/fiscal-attestations/{child.Id}/{location.Id}/2027/regenerate", org.AccessToken));
        var regenerated = (await regenerateResponse.Content.ReadFromJsonAsync<FiscalAttestationResponse>())!;

        Assert.Equal(HttpStatusCode.OK, regenerateResponse.StatusCode);
        Assert.Equal(original.Id, regenerated.Id);
        Assert.True(regenerated.TotalAmountCents > original.TotalAmountCents);
        Assert.True(regenerated.GeneratedAt > original.GeneratedAt);

        var secondListResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var afterList = (await secondListResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        Assert.Single(afterList); // no duplicate row for the same (child, location, year)
    }

    [Fact]
    public async Task Regenerate_DoesNotAffectOtherChildrensAttestations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, childA.Id, 2027, 1);
        var childB = await CreateChildAsync(client, org.AccessToken, "Lucas");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, childB.Id, 2027, 1, weekday: DayOfWeek.Tuesday);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var beforeList = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken)))
            .Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        var childBBefore = beforeList.Single(a => a.ChildId == childB.Id);

        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, childA.Id, 2027, 2);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/fiscal-attestations/{childA.Id}/{location.Id}/2027/regenerate", org.AccessToken));

        var afterList = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken)))
            .Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        var childBAfter = afterList.Single(a => a.ChildId == childB.Id);

        Assert.Equal(childBBefore.TotalAmountCents, childBAfter.TotalAmountCents);
        // Millisecond-tolerant: a DateTime round-tripped through PostgreSQL's timestamptz twice
        // can differ by a few ticks (same precision class as RegenerateInvoiceTests, 014).
        Assert.True(Math.Abs((childBBefore.GeneratedAt!.Value - childBAfter.GeneratedAt!.Value).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task Regenerate_NoPaidInvoices_Returns422_CreatesNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/fiscal-attestations/{child.Id}/{location.Id}/2027/regenerate", org.AccessToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("errors.fiscalAttestation.no_paid_invoices", body);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!;
        Assert.Empty(list);
    }

    [Fact]
    public async Task Regenerate_SendsASecondNotification()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, contact, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var contactRow = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
        contactRow.PushToken = "ExponentPushToken[fiscal-regenerate-test]";
        await db.SaveChangesAsync();

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        // Filtered by push token AND in-app Notification Type, not just token — the same
        // contact also receives invoice-sent/invoice-paid pushes (014/014a) to the same token,
        // which must not be miscounted here.
        var countAfterGenerate = await CountFiscalAttestationNotificationsAsync(db, contactRow.TenantUserId!.Value);

        // Reuses the same already-active, open-ended contract — a second CreateContractRequest
        // for the same child/location would fail activation on overlap.
        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/fiscal-attestations/{child.Id}/{location.Id}/2027/regenerate", org.AccessToken));
        var countAfterRegenerate = await CountFiscalAttestationNotificationsAsync(db, contactRow.TenantUserId!.Value);
        Assert.Equal(1, countAfterGenerate);
        Assert.Equal(2, countAfterRegenerate);
        // The push side also fired at least once — best-effort dispatch actually happened, not
        // just the in-app Notification row.
        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[fiscal-regenerate-test]");
    }

    private static Task<int> CountFiscalAttestationNotificationsAsync(Application.Common.ITenantDbContext db, Guid tenantUserId) =>
        db.Notifications.CountAsync(
            n => n.TenantUserId == tenantUserId && n.Type == Domain.Enums.NotificationType.FiscalAttestationGenerated);

    [Fact]
    public async Task Regenerate_ParentFacingListReflectsTheCorrectedVersion_NotStale()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Regenerate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));

        var beforeResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/fiscal-attestations", parentToken));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        await CreatePaidInvoiceForExistingContractAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 2);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/fiscal-attestations/{child.Id}/{location.Id}/2027/regenerate", org.AccessToken));

        var afterResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/parent/fiscal-attestations", parentToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        Assert.True(after.TotalAmountCents > before.TotalAmountCents);
        Assert.True(after.GeneratedAt > before.GeneratedAt);
        Assert.Equal(before.Id, after.Id);
    }
}
