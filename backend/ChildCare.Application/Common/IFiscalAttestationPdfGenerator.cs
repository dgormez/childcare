using ChildCare.Application.FiscalAttestations;

namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering a fiscal attestation as a PDF (constitution's fixed QuestPDF library).
/// Mirrors IInvoicePdfGenerator's port/adapter split, but — unlike invoices/betalingsbewijs —
/// the rendered bytes are persisted to GCS rather than rendered on demand (research.md R1); the
/// caller (FiscalAttestationWriter) is responsible for the upload. This model deliberately has
/// no field that could hold an NRN/SSIN (spec.md FR-007) — the PDF's blank NRN field is a fixed
/// part of the rendered template, never populated from model data.
/// </summary>
public interface IFiscalAttestationPdfGenerator
{
    Task<byte[]> GenerateAsync(FiscalAttestationPdfModel model, CancellationToken cancellationToken = default);
}

public record FiscalAttestationPdfModel(
    string LocationName,
    string LocationAddress,
    string? KboNumber,
    string? Erkenningsnummer,
    string ParentName,
    string ChildFirstName,
    string ChildLastName,
    DateOnly ChildDateOfBirth,
    int TaxYear,
    IReadOnlyList<FiscalAttestationPeriod> Periods,
    int TotalAmountCents,
    string Locale);
