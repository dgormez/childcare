using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders a contract as a PDF via QuestPDF (constitution's fixed Phase 1 PDF library,
/// research.md R4). All static labels are resolved from ContractPdfModel.Locale — never
/// hardcoded in one language (constitution Principle IV, FR-011/FR-016).
/// </summary>
public class QuestPdfContractGenerator : IContractPdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Opvangovereenkomst",
            ["child"] = "Kind",
            ["location"] = "Locatie",
            ["status"] = "Status",
            ["contractedDays"] = "Contractdagen",
            ["dailyRate"] = "Dagtarief",
            ["consent"] = "Toestemming foto/video",
            ["photosInternal"] = "Foto's intern gebruik",
            ["photosWebsite"] = "Foto's website",
            ["photosSocialMedia"] = "Foto's sociale media",
            ["videoInternal"] = "Video intern gebruik",
            ["photosPress"] = "Foto's pers",
            ["signature"] = "Handtekening",
            ["yes"] = "Ja",
            ["no"] = "Nee",
        },
        ["fr"] = new()
        {
            ["title"] = "Contrat d'accueil",
            ["child"] = "Enfant",
            ["location"] = "Lieu",
            ["status"] = "Statut",
            ["contractedDays"] = "Jours contractuels",
            ["dailyRate"] = "Tarif journalier",
            ["consent"] = "Consentement photo/vidéo",
            ["photosInternal"] = "Photos usage interne",
            ["photosWebsite"] = "Photos site web",
            ["photosSocialMedia"] = "Photos réseaux sociaux",
            ["videoInternal"] = "Vidéo usage interne",
            ["photosPress"] = "Photos presse",
            ["signature"] = "Signature",
            ["yes"] = "Oui",
            ["no"] = "Non",
        },
        ["en"] = new()
        {
            ["title"] = "Enrolment Contract",
            ["child"] = "Child",
            ["location"] = "Location",
            ["status"] = "Status",
            ["contractedDays"] = "Contracted Days",
            ["dailyRate"] = "Daily Rate",
            ["consent"] = "Photo/Video Consent",
            ["photosInternal"] = "Photos internal use",
            ["photosWebsite"] = "Photos website",
            ["photosSocialMedia"] = "Photos social media",
            ["videoInternal"] = "Video internal use",
            ["photosPress"] = "Photos press",
            ["signature"] = "Signature",
            ["yes"] = "Yes",
            ["no"] = "No",
        },
    };

    public Task<byte[]> GenerateAsync(ContractPdfModel model, CancellationToken cancellationToken = default)
    {
        var t = Labels.TryGetValue(model.Locale, out var dict) ? dict : Labels["nl"];

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Text(t["title"]).FontSize(18).Bold();

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text($"{t["child"]}: {model.ChildName}");
                    column.Item().Text($"{t["location"]}: {model.LocationName}");
                    column.Item().Text($"{t["status"]}: {model.Status}");

                    column.Item().Text(t["contractedDays"]).Bold();
                    foreach (var day in model.ContractedDays)
                        column.Item().Text($"{day.Weekday}: {day.StartTime:HH\\:mm} - {day.EndTime:HH\\:mm}");

                    column.Item().Text($"{t["dailyRate"]}: {model.DailyRateCents / 100.0:0.00}");

                    column.Item().Text(t["consent"]).Bold();
                    column.Item().Text($"{t["photosInternal"]}: {(model.PhotosInternal ? t["yes"] : t["no"])}");
                    column.Item().Text($"{t["photosWebsite"]}: {(model.PhotosWebsite ? t["yes"] : t["no"])}");
                    column.Item().Text($"{t["photosSocialMedia"]}: {(model.PhotosSocialMedia ? t["yes"] : t["no"])}");
                    column.Item().Text($"{t["videoInternal"]}: {(model.VideoInternal ? t["yes"] : t["no"])}");
                    column.Item().Text($"{t["photosPress"]}: {(model.PhotosPress ? t["yes"] : t["no"])}");

                    column.Item().PaddingTop(30).Text($"{t["signature"]}: _______________________");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
