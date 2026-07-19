namespace ChildCare.Application.Common;

/// <summary>
/// Feature 030 (US3) — port for rendering a combined family invoice as a PDF (constitution's
/// fixed QuestPDF library). Mirrors IInvoicePdfGenerator's port/adapter split, extended to loop
/// per-child sections instead of assuming one child (research.md R5).
/// </summary>
public interface IFamilyInvoicePdfGenerator
{
    Task<byte[]> GenerateAsync(FamilyInvoicePdfModel model, CancellationToken cancellationToken = default);
}

public record FamilyInvoicePdfChildSection(
    string ChildName,
    int PresentDays,
    int UnjustifiedAbsentDays,
    int DailyRateCents,
    IReadOnlyList<InvoicePdfExtraCharge> ExtraCharges,
    int TotalCents,
    string OgmReference);

public record FamilyInvoicePdfModel(
    string LocationName,
    string LocationAddress,
    string? KboNumber,
    string? Erkenningsnummer,
    string? BankAccountNumber,
    string ParentName,
    int PeriodYear,
    int PeriodMonth,
    IReadOnlyList<FamilyInvoicePdfChildSection> Children,
    int CombinedTotalCents,
    DateOnly? DueDate,
    string Locale);
