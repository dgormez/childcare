using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders a child's milestone portfolio as a PDF via QuestPDF (constitution's fixed PDF
/// library). Mirrors QuestPdfInvoiceGenerator's per-locale Labels dictionary pattern exactly.
/// Rendered on-demand, never persisted (research.md R4).
/// </summary>
public class QuestPdfMilestonePortfolioGenerator : IMilestonePortfolioPdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Ontwikkelingsportfolio",
            ["child"] = "Kind",
            ["currentFocus"] = "Huidige focus",
            ["noObservations"] = "Nog niet geregistreerd",
        },
        ["fr"] = new()
        {
            ["title"] = "Portfolio de développement",
            ["child"] = "Enfant",
            ["currentFocus"] = "Focus actuel",
            ["noObservations"] = "Pas encore enregistré",
        },
        ["en"] = new()
        {
            ["title"] = "Development Portfolio",
            ["child"] = "Child",
            ["currentFocus"] = "Current focus",
            ["noObservations"] = "Not recorded yet",
        },
    };

    public Task<byte[]> GenerateAsync(MilestonePortfolioPdfModel model, CancellationToken cancellationToken = default)
    {
        var t = Labels.TryGetValue(model.Locale, out var dict) ? dict : Labels["nl"];

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text(t["title"]).FontSize(18).Bold();
                    column.Item().Text($"{t["child"]}: {model.ChildName}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    foreach (var domain in model.Portfolio.Domains)
                    {
                        var domainName = model.Locale switch
                        {
                            "fr" => domain.NameFr,
                            "en" => domain.NameEn,
                            _ => domain.NameNl,
                        };

                        column.Item().PaddingTop(8).Text(domainName).Bold().FontSize(13);

                        foreach (var milestone in domain.Milestones)
                        {
                            var description = model.Locale switch
                            {
                                "fr" => milestone.DescriptionFr,
                                "en" => milestone.DescriptionEn,
                                _ => milestone.DescriptionNl,
                            };

                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text(description);
                                row.ConstantItem(120).AlignRight().Text(milestone.CurrentStatus ?? t["noObservations"]);
                                if (milestone.IsCurrentFocus)
                                    row.ConstantItem(90).AlignRight().Text(t["currentFocus"]).Bold();
                            });
                        }
                    }
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
