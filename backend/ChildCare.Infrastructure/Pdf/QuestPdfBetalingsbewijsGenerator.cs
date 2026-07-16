using System.Globalization;
using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders a betalingsbewijs (payment receipt) via QuestPDF. Mirrors
/// QuestPdfInvoiceGenerator's per-locale Labels dictionary pattern exactly (constitution
/// Principle IV, spec.md FR-020).
/// </summary>
public class QuestPdfBetalingsbewijsGenerator : IBetalingsbewijsGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Betalingsbewijs",
            ["parent"] = "Ouder",
            ["child"] = "Kind",
            ["reference"] = "Gestructureerde mededeling",
            ["amountPaid"] = "Betaald bedrag",
            ["paidOn"] = "Betaald op",
        },
        ["fr"] = new()
        {
            ["title"] = "Preuve de paiement",
            ["parent"] = "Parent",
            ["child"] = "Enfant",
            ["reference"] = "Communication structurée",
            ["amountPaid"] = "Montant payé",
            ["paidOn"] = "Payé le",
        },
        ["en"] = new()
        {
            ["title"] = "Payment receipt",
            ["parent"] = "Parent",
            ["child"] = "Child",
            ["reference"] = "Structured payment reference",
            ["amountPaid"] = "Amount paid",
            ["paidOn"] = "Paid on",
        },
    };

    public Task<byte[]> GenerateAsync(BetalingsbewijsModel model, CancellationToken cancellationToken = default)
    {
        var t = Labels.TryGetValue(model.Locale, out var dict) ? dict : Labels["nl"];
        var culture = CultureInfo.GetCultureInfo(model.Locale == "en" ? "en-US" : model.Locale);

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
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().PaddingTop(16).Text($"{t["parent"]}: {model.ParentName}");
                    column.Item().Text($"{t["child"]}: {model.ChildName}");
                    column.Item().Text($"{t["reference"]}: {model.OgmReference}");
                    column.Item().PaddingTop(12).Text($"{t["amountPaid"]}: {model.AmountPaidCents / 100.0:0.00}").Bold().FontSize(14);
                    column.Item().Text($"{t["paidOn"]}: {model.PaidAt.ToString("d", culture)}");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
