namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering an invoice as a PDF (constitution's fixed QuestPDF library). Mirrors
/// IContractPdfGenerator's port/adapter split exactly — rendered on-demand from the invoice's
/// current stored state, never persisted to storage (research.md R1).
/// </summary>
public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateAsync(InvoicePdfModel model, CancellationToken cancellationToken = default);
}

public record InvoicePdfModel(
    string LocationName,
    string LocationAddress,
    string? KboNumber,
    string? Erkenningsnummer,
    string? BankAccountNumber,
    string ParentName,
    string ChildName,
    int PeriodYear,
    int PeriodMonth,
    int PresentDays,
    int UnjustifiedAbsentDays,
    int DailyRateCents,
    IReadOnlyList<InvoicePdfExtraCharge> ExtraCharges,
    int TotalCents,
    DateOnly? DueDate,
    string OgmReference,
    string Locale);

public record InvoicePdfExtraCharge(string Label, int AmountCents);
