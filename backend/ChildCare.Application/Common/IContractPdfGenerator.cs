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
    string Locale);

public record ContractPdfDay(DayOfWeek Weekday, TimeOnly StartTime, TimeOnly EndTime);
