using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.SepaBatches;

/// <summary>
/// Shared helpers for feature 026's API test suite. Reuses feature 025's
/// CodaTransactionsTestSupport for contract/invoice setup (CreateAndActivateContractAsync,
/// CreateSentInvoiceAsync) rather than duplicating it.
/// </summary>
internal static class SepaBatchesTestSupport
{
    /// <summary>Directly seeds a full, signed SEPA mandate on a contract (IBAN + mandate
    /// reference + authorised-at) — bypasses feature 024's full e-signature ceremony, which
    /// isn't what this feature's tests exercise. Mirrors CodaTransactionsTestSupport's
    /// SeedContractSepaIbanAsync but also sets the two fields FR-001's eligibility rule needs
    /// that CODA's own tests never had to populate (SepaMandateReference, SepaAuthorisedAt).</summary>
    public static async Task<string> SeedFullSepaMandateAsync(
        IServiceProvider services, string schemaName, Guid contractId, string iban, DateTime? authorisedAt = null)
    {
        using var scope = services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
        var protector = scope.ServiceProvider.GetRequiredService<IIbanProtector>();
        var db = resolver.ForSchema(schemaName);

        var contract = await db.Contracts.FirstAsync(c => c.Id == contractId);
        var normalized = iban.Replace(" ", string.Empty).ToUpperInvariant();
        var mandateReference = $"MND-TEST-{Guid.NewGuid():N}"[..16];
        contract.SepaIbanEncrypted = protector.Protect(normalized);
        contract.SepaIbanLast4 = normalized[^4..];
        contract.SepaMandateReference = mandateReference;
        contract.SepaAuthorisedAt = authorisedAt ?? DateTime.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();
        return mandateReference;
    }

    /// <summary>Seeds a primary ChildContact/Contact directly — pain.008's debtor name (research.md
    /// R6) requires a non-empty Dbtr/Nm, but the shared child-fixture helpers (ChildEventTestSupport.
    /// CreateChildAsync) don't create one, since no other feature's tests needed a real name.</summary>
    public static async Task SeedPrimaryContactAsync(IServiceProvider services, string schemaName, Guid childId, string firstName = "Test", string lastName = "Parent")
    {
        using var scope = services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);

        var contact = new Domain.Entities.Contact { FirstName = firstName, LastName = lastName, Phone = "+32470000000" };
        db.Contacts.Add(contact);
        db.ChildContacts.Add(new Domain.Entities.ChildContact
        {
            ChildId = childId,
            ContactId = contact.Id,
            Relationship = Domain.Enums.ContactRelationship.Mother,
            CanPickup = true,
            IsPrimary = true,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Configures the two pre-existing fields batch generation reads as creditor
    /// headers (research.md R5) — the organisation's SEPA Creditor Identifier (feature 024) and
    /// the location's bank account (feature 014). No new settings surface for feature 026.</summary>
    public static async Task ConfigureSepaCreditorAsync(HttpClient client, string accessToken, Guid locationId, string creditorIdentifier, string creditorIban)
    {
        var orgResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/organisations/me", accessToken, new UpdateOrganisationRequest(null, creditorIdentifier)));
        Assert.Equal(HttpStatusCode.OK, orgResponse.StatusCode);

        var locationResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{locationId}/invoice-settings", accessToken,
            new UpdateLocationInvoiceSettingsRequest(null, creditorIban, 14)));
        Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode);
    }

    public static async Task<SepaBatchEligibilityResponse> GetEligibilityAsync(HttpClient client, string accessToken, Guid locationId, int year, int month)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/locations/{locationId}/sepa-batch-eligibility?month={year:D4}-{month:D2}-01", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SepaBatchEligibilityResponse>())!;
    }

    public static async Task<HttpResponseMessage> GenerateBatchAsync(
        HttpClient client, string accessToken, Guid locationId, IReadOnlyList<Guid> invoiceIds, DateOnly executionDate) =>
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{locationId}/sepa-batches", accessToken,
            new GenerateSepaBatchRequest(invoiceIds, executionDate)));

    /// <summary>The next Monday-Friday date strictly after today — a valid execution date for
    /// every happy-path test regardless of what day the suite happens to run (024's own
    /// shipped-note precedent: never hardcode a relative-date offset without a weekend check).</summary>
    public static DateOnly NextBusinessDay()
    {
        var candidate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);
        return candidate;
    }
}
