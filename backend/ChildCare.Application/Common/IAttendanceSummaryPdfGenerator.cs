using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering the monthly attendance summary as a PDF (constitution's fixed QuestPDF
/// library). Mirrors IInvoicePdfGenerator's on-demand, unstored pattern exactly — this is a
/// point-in-time export, not a permanent per-child legal record like FiscalAttestation
/// (research.md R5).
/// </summary>
public interface IAttendanceSummaryPdfGenerator
{
    byte[] Generate(AttendanceSummaryResponse summary, string locale);
}
