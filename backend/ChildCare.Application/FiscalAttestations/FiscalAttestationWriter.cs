using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

/// <summary>
/// Shared "render + store + upsert" operation both GenerateFiscalAttestationsCommand (US1, only
/// called for children without an existing row — spec.md FR-009) and
/// RegenerateFiscalAttestationCommand (US3, always overwrites — spec.md FR-008) build on.
/// Reuses the existing row's Id on regenerate so the GCS object at the same deterministic path
/// gets overwritten, not duplicated (research.md R1).
/// </summary>
public class FiscalAttestationWriter(
    ITenantDbContext db,
    IPublicDbContext publicDb,
    ICurrentTenantService currentTenant,
    FiscalAttestationAggregator aggregator,
    IFiscalAttestationPdfGenerator pdfGenerator,
    IFiscalAttestationStorage storage)
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<FiscalAttestationWriteResult> WriteAsync(
        Guid childId, Guid locationId, int taxYear, CancellationToken cancellationToken = default)
    {
        var aggregation = await aggregator.AggregateAsync(childId, locationId, taxYear, cancellationToken);
        if (aggregation.Periods.Count == 0)
            return FiscalAttestationWriteResult.NoPaidInvoices();

        var existing = await db.FiscalAttestations.FirstOrDefaultAsync(
            fa => fa.ChildId == childId && fa.LocationId == locationId && fa.TaxYear == taxYear, cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == childId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == locationId, cancellationToken);
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);

        // Locale is chosen once at generation/regeneration time from the child's primary
        // contact — unlike invoice PDFs (rendered fresh per-request with a caller-supplied
        // locale), a persisted attestation has exactly one stored rendering (research.md R1).
        var primaryContact = await db.ChildContacts
            .Where(cc => cc.ChildId == childId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .FirstOrDefaultAsync(cancellationToken);

        var locale = primaryContact is not null && SupportedLocales.Contains(primaryContact.Locale) ? primaryContact.Locale : "nl";

        var attestationId = existing?.Id ?? Guid.NewGuid();

        var model = new FiscalAttestationPdfModel(
            location.Name,
            location.Address,
            tenant.KboNumber,
            location.Erkenningsnummer,
            primaryContact is null ? string.Empty : $"{primaryContact.FirstName} {primaryContact.LastName}",
            child.FirstName,
            child.LastName,
            child.DateOfBirth,
            taxYear,
            aggregation.Periods,
            aggregation.TotalAmountCents,
            locale);

        var pdfBytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        var objectPath = await storage.UploadAsync(attestationId, pdfBytes, cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            var attestation = new FiscalAttestation
            {
                Id = attestationId,
                ChildId = childId,
                LocationId = locationId,
                TaxYear = taxYear,
                Periods = FiscalAttestationPeriods.ToJson(aggregation.Periods),
                TotalAmountCents = aggregation.TotalAmountCents,
                PdfObjectPath = objectPath,
                GeneratedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.FiscalAttestations.Add(attestation);
            await db.SaveChangesAsync(cancellationToken);
            return FiscalAttestationWriteResult.Success(attestation);
        }

        existing.Periods = FiscalAttestationPeriods.ToJson(aggregation.Periods);
        existing.TotalAmountCents = aggregation.TotalAmountCents;
        existing.PdfObjectPath = objectPath;
        existing.GeneratedAt = now;
        existing.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return FiscalAttestationWriteResult.Success(existing);
    }
}

public class FiscalAttestationWriteResult
{
    public FiscalAttestation? Attestation { get; private init; }
    public bool HadPaidInvoices { get; private init; } = true;
    public bool Succeeded => HadPaidInvoices;

    public static FiscalAttestationWriteResult Success(FiscalAttestation attestation) => new() { Attestation = attestation };
    public static FiscalAttestationWriteResult NoPaidInvoices() => new() { HadPaidInvoices = false };
}
