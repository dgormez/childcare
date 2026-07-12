namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering a single incident report as a PDF (constitution's fixed QuestPDF library).
/// Mirrors IContractPdfGenerator's port/adapter split exactly (research.md R2) — bytes are
/// streamed directly, never uploaded to GCS, matching feature 007's contract-PDF precedent.
/// </summary>
public interface IIncidentReportPdfGenerator
{
    Task<byte[]> GenerateAsync(IncidentReportPdfModel model, CancellationToken cancellationToken = default);
}

public record IncidentReportPdfModel(
    string ChildName,
    string LocationName,
    string? LocationAddress,
    string? LocationDossiernummer,
    DateTime OccurredAt,
    DateTime CreatedAt,
    string? LocationDetail,
    string Description,
    string InjuryType,
    string? FirstAidGiven,
    bool DoctorCalled,
    string? DoctorNotes,
    bool ParentNotified,
    DateTime? ParentNotifiedAt,
    string? ParentNotifiedHow,
    string? Witnesses,
    string? FollowUp,
    string Locale);
