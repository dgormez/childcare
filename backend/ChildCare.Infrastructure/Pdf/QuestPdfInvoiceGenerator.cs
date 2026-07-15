using System.Globalization;
using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders an invoice as a PDF via QuestPDF (constitution's fixed Phase 1 PDF library).
/// Mirrors QuestPdfContractGenerator's per-locale Labels dictionary pattern exactly — every
/// static label resolved from InvoicePdfModel.Locale, never hardcoded in one language
/// (constitution Principle IV, spec.md FR-018).
/// </summary>
public class QuestPdfInvoiceGenerator : IInvoicePdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Factuur",
            ["kbo"] = "KBO-nummer",
            ["erkenningsnummer"] = "Erkenningsnummer",
            ["parent"] = "Ouder",
            ["child"] = "Kind",
            ["period"] = "Periode",
            ["presentDays"] = "Aanwezige dagen",
            ["unjustifiedAbsentDays"] = "Ongewettigde afwezigheden",
            ["dailyRate"] = "Dagtarief",
            ["total"] = "Totaal verschuldigd",
            ["dueDate"] = "Vervaldatum",
            ["ogmReference"] = "Gestructureerde mededeling",
            ["bankAccountNumber"] = "Rekeningnummer",
        },
        ["fr"] = new()
        {
            ["title"] = "Facture",
            ["kbo"] = "Numéro BCE",
            ["erkenningsnummer"] = "Numéro d'agrément",
            ["parent"] = "Parent",
            ["child"] = "Enfant",
            ["period"] = "Période",
            ["presentDays"] = "Jours de présence",
            ["unjustifiedAbsentDays"] = "Absences non justifiées",
            ["dailyRate"] = "Tarif journalier",
            ["total"] = "Total dû",
            ["dueDate"] = "Échéance",
            ["ogmReference"] = "Communication structurée",
            ["bankAccountNumber"] = "Numéro de compte",
        },
        ["en"] = new()
        {
            ["title"] = "Invoice",
            ["kbo"] = "Company registration number",
            ["erkenningsnummer"] = "License number",
            ["parent"] = "Parent",
            ["child"] = "Child",
            ["period"] = "Period",
            ["presentDays"] = "Present days",
            ["unjustifiedAbsentDays"] = "Unjustified absences",
            ["dailyRate"] = "Daily rate",
            ["total"] = "Total due",
            ["dueDate"] = "Due date",
            ["ogmReference"] = "Structured payment reference",
            ["bankAccountNumber"] = "Bank account number",
        },
    };

    public Task<byte[]> GenerateAsync(InvoicePdfModel model, CancellationToken cancellationToken = default)
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
                    column.Item().Text($"{t["child"]}: {model.ChildName}");
                    column.Item().Text($"{t["period"]}: {periodLabel}");

                    column.Item().PaddingTop(12).Text($"{t["presentDays"]}: {model.PresentDays}");
                    column.Item().Text($"{t["unjustifiedAbsentDays"]}: {model.UnjustifiedAbsentDays}");
                    column.Item().Text($"{t["dailyRate"]}: {model.DailyRateCents / 100.0:0.00}");

                    foreach (var charge in model.ExtraCharges)
                        column.Item().Text($"{charge.Label}: {charge.AmountCents / 100.0:0.00}");

                    column.Item().PaddingTop(12).Text($"{t["total"]}: {model.TotalCents / 100.0:0.00}").Bold().FontSize(14);
                    if (model.DueDate is { } dueDate)
                        column.Item().Text($"{t["dueDate"]}: {dueDate:yyyy-MM-dd}");

                    column.Item().PaddingTop(16).Text($"{t["ogmReference"]}: {model.OgmReference}").Bold().FontSize(14);
                    if (model.BankAccountNumber is not null)
                        column.Item().Text($"{t["bankAccountNumber"]}: {model.BankAccountNumber}");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
