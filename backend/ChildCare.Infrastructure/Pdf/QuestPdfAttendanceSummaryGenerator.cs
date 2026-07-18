using System.Globalization;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders the monthly attendance summary as a PDF via QuestPDF (constitution's fixed PDF
/// library). Mirrors QuestPdfInvoiceGenerator's per-locale Labels dictionary pattern exactly —
/// on-demand, never persisted (research.md R5).
/// </summary>
public class QuestPdfAttendanceSummaryGenerator : IAttendanceSummaryPdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Maandelijks aanwezigheidsoverzicht",
            ["period"] = "Periode",
            ["child"] = "Kind",
            ["present"] = "Aanwezig",
            ["absentJustified"] = "Afwezig (gewettigd)",
            ["absentUnjustified"] = "Afwezig (ongewettigd)",
            ["closure"] = "Sluiting",
        },
        ["fr"] = new()
        {
            ["title"] = "Résumé mensuel des présences",
            ["period"] = "Période",
            ["child"] = "Enfant",
            ["present"] = "Présent",
            ["absentJustified"] = "Absent (justifié)",
            ["absentUnjustified"] = "Absent (non justifié)",
            ["closure"] = "Fermeture",
        },
        ["en"] = new()
        {
            ["title"] = "Monthly attendance summary",
            ["period"] = "Period",
            ["child"] = "Child",
            ["present"] = "Present",
            ["absentJustified"] = "Absent (justified)",
            ["absentUnjustified"] = "Absent (unjustified)",
            ["closure"] = "Closure",
        },
    };

    public byte[] Generate(AttendanceSummaryResponse summary, string locale)
    {
        var t = Labels.TryGetValue(locale, out var dict) ? dict : Labels["nl"];
        var culture = CultureInfo.GetCultureInfo(locale == "en" ? "en-US" : locale);
        var periodLabel = summary.Month.ToString("MMMM yyyy", culture);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text(t["title"]).FontSize(18).Bold();
                    column.Item().Text($"{t["period"]}: {periodLabel}");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text(t["child"]).Bold();
                        header.Cell().Text(t["present"]).Bold();
                        header.Cell().Text(t["absentJustified"]).Bold();
                        header.Cell().Text(t["absentUnjustified"]).Bold();
                        header.Cell().Text(t["closure"]).Bold();
                    });

                    foreach (var row in summary.Children)
                    {
                        table.Cell().Text(row.ChildName);
                        table.Cell().Text(row.PresentDays.ToString());
                        table.Cell().Text(row.AbsentJustifiedDays.ToString());
                        table.Cell().Text(row.AbsentUnjustifiedDays.ToString());
                        table.Cell().Text(row.ClosureDays.ToString());
                    }
                });
            });
        });

        return document.GeneratePdf();
    }
}
