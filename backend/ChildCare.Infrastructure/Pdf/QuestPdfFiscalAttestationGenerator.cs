using System.Globalization;
using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders a fiscal attestation as a PDF via QuestPDF (constitution's fixed PDF library).
/// Mirrors QuestPdfInvoiceGenerator's per-locale Labels dictionary pattern exactly (constitution
/// Principle IV, spec.md FR-013). The NRN field is always rendered blank — FiscalAttestationPdfModel
/// has no field that could hold one (spec.md FR-007), so there is nothing to leak here even if a
/// caller wanted to. Declaration wording approximates the Opgroeien official template (spec.md
/// FR-006/Assumptions — exact legal text is a content detail to verify against the official
/// document before production use, not a scope blocker for this feature).
/// </summary>
public class QuestPdfFiscalAttestationGenerator : IFiscalAttestationPdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Fiscaal attest kinderopvang",
            ["kbo"] = "KBO-nummer",
            ["erkenningsnummer"] = "Erkenningsnummer",
            ["parent"] = "Ouder",
            ["child"] = "Kind",
            ["dateOfBirth"] = "Geboortedatum",
            ["taxYear"] = "Aanslagjaar",
            ["nrn"] = "Rijksregisternummer (in te vullen door de ouder)",
            ["periodStart"] = "Van",
            ["periodEnd"] = "Tot",
            ["days"] = "Dagen",
            ["dailyRate"] = "Dagtarief",
            ["amount"] = "Bedrag",
            ["total"] = "Totaal betaald bedrag",
            ["certificationType"] = "Type attest: 1 (opvang georganiseerd of gesubsidieerd door Opgroeien)",
            ["declaration"] = "Hierbij wordt bevestigd dat bovenvermeld bedrag werd betaald voor de opvang van het genoemde kind, opvang die beantwoordt aan de voorwaarden voor fiscale aftrek van kosten voor kinderopvang.",
            ["signature"] = "Handtekening verantwoordelijke",
        },
        ["fr"] = new()
        {
            ["title"] = "Attestation fiscale garde d'enfants",
            ["kbo"] = "Numéro BCE",
            ["erkenningsnummer"] = "Numéro d'agrément",
            ["parent"] = "Parent",
            ["child"] = "Enfant",
            ["dateOfBirth"] = "Date de naissance",
            ["taxYear"] = "Exercice d'imposition",
            ["nrn"] = "Numéro de registre national (à compléter par le parent)",
            ["periodStart"] = "Du",
            ["periodEnd"] = "Au",
            ["days"] = "Jours",
            ["dailyRate"] = "Tarif journalier",
            ["amount"] = "Montant",
            ["total"] = "Montant total payé",
            ["certificationType"] = "Type d'attestation : 1 (accueil organisé ou subventionné par Opgroeien)",
            ["declaration"] = "Il est certifié que le montant ci-dessus a été payé pour la garde de l'enfant mentionné, garde répondant aux conditions de déduction fiscale des frais de garde d'enfants.",
            ["signature"] = "Signature du responsable",
        },
        ["en"] = new()
        {
            ["title"] = "Childcare fiscal attestation",
            ["kbo"] = "Company registration number",
            ["erkenningsnummer"] = "License number",
            ["parent"] = "Parent",
            ["child"] = "Child",
            ["dateOfBirth"] = "Date of birth",
            ["taxYear"] = "Tax year",
            ["nrn"] = "National registry number (to be completed by the parent)",
            ["periodStart"] = "From",
            ["periodEnd"] = "To",
            ["days"] = "Days",
            ["dailyRate"] = "Daily rate",
            ["amount"] = "Amount",
            ["total"] = "Total amount paid",
            ["certificationType"] = "Certification type: 1 (care organized or subsidized by Opgroeien)",
            ["declaration"] = "This certifies that the amount above was paid for the care of the named child, care that meets the conditions for the fiscal deduction of childcare costs.",
            ["signature"] = "Signature of the responsible person",
        },
    };

    public Task<byte[]> GenerateAsync(FiscalAttestationPdfModel model, CancellationToken cancellationToken = default)
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
                    if (model.KboNumber is not null)
                        column.Item().Text($"{t["kbo"]}: {model.KboNumber}");
                    if (model.Erkenningsnummer is not null)
                        column.Item().Text($"{t["erkenningsnummer"]}: {model.Erkenningsnummer}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().PaddingTop(16).Text($"{t["parent"]}: {model.ParentName}");
                    column.Item().Text($"{t["child"]}: {model.ChildFirstName} {model.ChildLastName}");
                    column.Item().Text($"{t["dateOfBirth"]}: {model.ChildDateOfBirth:yyyy-MM-dd}");
                    column.Item().Text($"{t["taxYear"]}: {model.TaxYear}");
                    column.Item().PaddingTop(4).Text($"{t["nrn"]}: ________________________");

                    column.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(t["periodStart"]).Bold();
                            header.Cell().Text(t["periodEnd"]).Bold();
                            header.Cell().Text(t["days"]).Bold();
                            header.Cell().Text(t["dailyRate"]).Bold();
                            header.Cell().Text(t["amount"]).Bold();
                        });

                        foreach (var period in model.Periods)
                        {
                            table.Cell().Text(period.PeriodStart.ToString("yyyy-MM-dd", culture));
                            table.Cell().Text(period.PeriodEnd.ToString("yyyy-MM-dd", culture));
                            table.Cell().Text(period.Days.ToString(culture));
                            table.Cell().Text(period.DailyRateCents.HasValue ? (period.DailyRateCents.Value / 100.0).ToString("0.00", culture) : "-");
                            table.Cell().Text((period.AmountCents / 100.0).ToString("0.00", culture));
                        }
                    });

                    column.Item().PaddingTop(12).Text($"{t["total"]}: {(model.TotalAmountCents / 100.0).ToString("0.00", culture)}").Bold().FontSize(14);

                    column.Item().PaddingTop(16).Text(t["certificationType"]);
                    column.Item().PaddingTop(8).Text(t["declaration"]);

                    column.Item().PaddingTop(32).Text($"{t["signature"]}: ________________________");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
