namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering an enrolment contract as a PDF (constitution's fixed QuestPDF library,
/// research.md R4). Mirrors IProfilePhotoStorage's port/adapter split — ChildCare.Application
/// depends only on this interface, never on QuestPDF directly.
/// </summary>
public interface IContractPdfGenerator
{
    Task<byte[]> GenerateAsync(ContractPdfModel model, CancellationToken cancellationToken = default);
}

public record ContractPdfModel(
    string ChildName,
    string LocationName,
    string Status,
    IReadOnlyList<ContractPdfDay> ContractedDays,
    int DailyRateCents,
    bool PhotosInternal,
    bool PhotosWebsite,
    bool PhotosSocialMedia,
    bool VideoInternal,
    bool PhotosPress,
    string Locale,
    // Feature 024-esignature — null for the existing unsigned, on-demand PDF (GenerateContractPdfQuery,
    // unchanged); populated only when rendering the final signed PDF (SubmitContractSigningCommand).
    ContractPdfSignature? Signature = null);

public record ContractPdfDay(DayOfWeek Weekday, TimeOnly StartTime, TimeOnly EndTime);

// Feature 024-esignature. IbanMasked is display-only (e.g. "•••• 0166") — the decrypted IBAN
// is never passed to the PDF generator in full beyond what's already committed to the stored
// document at signing time (FR-020's masking intent extends to this rendering, not just API
// responses).
public record ContractPdfSignature(
    DateTime SignedAtUtc,
    string SignatureType,
    string SignatureData,
    string SignedByIp,
    string SepaMandateReference,
    string SepaIbanMasked,
    string SepaCreditorIdentifier,
    DateTime SepaAuthorisedAtUtc);
