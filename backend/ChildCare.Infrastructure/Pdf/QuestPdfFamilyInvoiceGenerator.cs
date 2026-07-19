using System.Globalization;
using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Feature 030 (US3) — renders a combined family invoice PDF via QuestPDF. Mirrors
/// QuestPdfInvoiceGenerator's per-locale Labels dictionary pattern exactly, extended to loop a
/// per-child section instead of assuming one child (research.md R5).
/// </summary>
public class QuestPdfFamilyInvoiceGenerator : IFamilyInvoicePdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Gezinsfactuur",
            ["kbo"] = "KBO-nummer",
            ["erkenningsnummer"] = "Erkenningsnummer",
            ["parent"] = "Ouder",
            ["period"] = "Periode",
            ["presentDays"] = "Aanwezige dagen",
            ["unjustifiedAbsentDays"] = "Ongewettigde afwezigheden",
            ["dailyRate"] = "Dagtarief",
            ["subtotal"] = "Subtotaal",
            ["combinedTotal"] = "Totaal verschuldigd (gezin)",
            ["dueDate"] = "Vervaldatum",
            ["ogmReference"] = "Gestructureerde mededeling",
            ["bankAccountNumber"] = "Rekeningnummer",
        },
        ["fr"] = new()
        {
            ["title"] = "Facture familiale",
            ["kbo"] = "Numéro BCE",
            ["erkenningsnummer"] = "Numéro d'agrément",
            ["parent"] = "Parent",
            ["period"] = "Période",
            ["presentDays"] = "Jours de présence",
            ["unjustifiedAbsentDays"] = "Absences non justifiées",
            ["dailyRate"] = "Tarif journalier",
            ["subtotal"] = "Sous-total",
            ["combinedTotal"] = "Total dû (famille)",
            ["dueDate"] = "Échéance",
            ["ogmReference"] = "Communication structurée",
            ["bankAccountNumber"] = "Numéro de compte",
        },
        ["en"] = new()
        {
            ["title"] = "Family invoice",
            ["kbo"] = "Company registration number",
            ["erkenningsnummer"] = "License number",
            ["parent"] = "Parent",
            ["period"] = "Period",
            ["presentDays"] = "Present days",
            ["unjustifiedAbsentDays"] = "Unjustified absences",
            ["dailyRate"] = "Daily rate",
            ["subtotal"] = "Subtotal",
            ["combinedTotal"] = "Combined total due",
            ["dueDate"] = "Due date",
            ["ogmReference"] = "Structured payment reference",
            ["bankAccountNumber"] = "Bank account number",
        },
    };

    public Task<byte[]> GenerateAsync(FamilyInvoicePdfModel model, CancellationToken cancellationToken = default)
    {
        var t = Labels.TryGetValue(model.Locale, out var dict) ? dict : Labels["nl"];
        var culture = CultureInfo.GetCultureInfo(model.Locale == "en" ? "en-US" : model.Locale);
        var periodLabel = new DateOnly(model.PeriodYear, model.PeriodMonth, 1).ToString("MMMM yyyy", culture);

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
                    column.Item().Text(model.LocationName).Bold();
                    column.Item().Text(model.LocationAddress);
                    if (model.KboNumber is not null)
                        column.Item().Text($"{t["kbo"]}: {model.KboNumber}");
                    if (model.Erkenningsnummer is not null)
                        column.Item().Text($"{t["erkenningsnummer"]}: {model.Erkenningsnummer}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().PaddingTop(16).Text($"{t["parent"]}: {model.ParentName}");
                    column.Item().Text($"{t["period"]}: {periodLabel}");

                    foreach (var child in model.Children)
                    {
                        column.Item().PaddingTop(12).Text(child.ChildName).Bold().FontSize(13);
                        column.Item().Text($"{t["presentDays"]}: {child.PresentDays}");
                        column.Item().Text($"{t["unjustifiedAbsentDays"]}: {child.UnjustifiedAbsentDays}");
                        column.Item().Text($"{t["dailyRate"]}: {child.DailyRateCents / 100.0:0.00}");

                        foreach (var charge in child.ExtraCharges)
                            column.Item().Text($"{charge.Label}: {charge.AmountCents / 100.0:0.00}");

                        column.Item().Text($"{t["subtotal"]}: {child.TotalCents / 100.0:0.00}").Bold();
                        column.Item().Text($"{t["ogmReference"]}: {child.OgmReference}");
                    }

                    column.Item().PaddingTop(16).Text($"{t["combinedTotal"]}: {model.CombinedTotalCents / 100.0:0.00}").Bold().FontSize(14);
                    if (model.DueDate is { } dueDate)
                        column.Item().Text($"{t["dueDate"]}: {dueDate:yyyy-MM-dd}");
                    if (model.BankAccountNumber is not null)
                        column.Item().Text($"{t["bankAccountNumber"]}: {model.BankAccountNumber}");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
