using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering a child's milestone portfolio as a PDF (constitution's fixed QuestPDF
/// library). Mirrors IInvoicePdfGenerator's port/adapter split exactly — rendered on-demand from
/// current data, never persisted to storage (research.md R4): a portfolio keeps growing as new
/// observations are recorded, so a stored snapshot would go stale immediately.
/// </summary>
public interface IMilestonePortfolioPdfGenerator
{
    Task<byte[]> GenerateAsync(MilestonePortfolioPdfModel model, CancellationToken cancellationToken = default);
}

public record MilestonePortfolioPdfModel(
    string ChildName,
    MilestonePortfolioResponse Portfolio,
    string Locale);
