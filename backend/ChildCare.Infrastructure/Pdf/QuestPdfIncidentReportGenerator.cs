using ChildCare.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChildCare.Infrastructure.Pdf;

/// <summary>
/// Renders an incident report as a PDF via QuestPDF, mirroring QuestPdfContractGenerator's
/// exact structure (research.md R2) — a signature line for the reporting caregiver, all labels
/// resolved from IncidentReportPdfModel.Locale (constitution Principle IV, FR-012/FR-016).
/// </summary>
public class QuestPdfIncidentReportGenerator : IIncidentReportPdfGenerator
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["nl"] = new()
        {
            ["title"] = "Incidentmelding",
            ["child"] = "Kind",
            ["location"] = "Locatie",
            ["address"] = "Adres",
            ["dossiernummer"] = "Dossiernummer",
            ["occurredAt"] = "Datum/tijd voorval",
            ["createdAt"] = "Datum/tijd registratie",
            ["locationDetail"] = "Plaats",
            ["description"] = "Beschrijving",
            ["injuryType"] = "Letseltype",
            ["firstAidGiven"] = "Eerste hulp",
            ["doctorCalled"] = "Arts gebeld",
            ["doctorNotes"] = "Notities arts",
            ["parentNotified"] = "Ouder geïnformeerd",
            ["parentNotifiedAt"] = "Tijdstip informeren ouder",
            ["parentNotifiedHow"] = "Wijze van informeren",
            ["witnesses"] = "Getuigen",
            ["followUp"] = "Vervolgnotitie",
            ["signature"] = "Handtekening begeleider",
            ["yes"] = "Ja",
            ["no"] = "Nee",
        },
        ["fr"] = new()
        {
            ["title"] = "Rapport d'incident",
            ["child"] = "Enfant",
            ["location"] = "Lieu",
            ["address"] = "Adresse",
            ["dossiernummer"] = "Numéro de dossier",
            ["occurredAt"] = "Date/heure de l'incident",
            ["createdAt"] = "Date/heure d'enregistrement",
            ["locationDetail"] = "Emplacement",
            ["description"] = "Description",
            ["injuryType"] = "Type de blessure",
            ["firstAidGiven"] = "Premiers secours",
            ["doctorCalled"] = "Médecin contacté",
            ["doctorNotes"] = "Notes du médecin",
            ["parentNotified"] = "Parent informé",
            ["parentNotifiedAt"] = "Heure d'information du parent",
            ["parentNotifiedHow"] = "Moyen d'information",
            ["witnesses"] = "Témoins",
            ["followUp"] = "Note de suivi",
            ["signature"] = "Signature de l'accompagnant",
            ["yes"] = "Oui",
            ["no"] = "Non",
        },
        ["en"] = new()
        {
            ["title"] = "Incident Report",
            ["child"] = "Child",
            ["location"] = "Location",
            ["address"] = "Address",
            ["dossiernummer"] = "Dossier Number",
            ["occurredAt"] = "Date/Time Occurred",
            ["createdAt"] = "Date/Time Recorded",
            ["locationDetail"] = "Location Detail",
            ["description"] = "Description",
            ["injuryType"] = "Injury Type",
            ["firstAidGiven"] = "First Aid Given",
            ["doctorCalled"] = "Doctor Called",
            ["doctorNotes"] = "Doctor Notes",
            ["parentNotified"] = "Parent Notified",
            ["parentNotifiedAt"] = "Parent Notified At",
            ["parentNotifiedHow"] = "Parent Notified How",
            ["witnesses"] = "Witnesses",
            ["followUp"] = "Follow-up Note",
            ["signature"] = "Reporting Caregiver Signature",
            ["yes"] = "Yes",
            ["no"] = "No",
        },
    };

    public Task<byte[]> GenerateAsync(IncidentReportPdfModel model, CancellationToken cancellationToken = default)
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
                    if (!string.IsNullOrWhiteSpace(model.LocationAddress))
                        column.Item().Text($"{t["address"]}: {model.LocationAddress}");
                    // FR-012: the PDF MUST render successfully with any unset optional field
                    // simply omitted, never as a rendering error — Dossiernummer is nullable.
                    column.Item().Text($"{t["dossiernummer"]}: {model.LocationDossiernummer ?? string.Empty}");

                    column.Item().Text($"{t["occurredAt"]}: {model.OccurredAt:yyyy-MM-dd HH:mm}");
                    column.Item().Text($"{t["createdAt"]}: {model.CreatedAt:yyyy-MM-dd HH:mm}");
                    if (!string.IsNullOrWhiteSpace(model.LocationDetail))
                        column.Item().Text($"{t["locationDetail"]}: {model.LocationDetail}");

                    column.Item().Text(t["description"]).Bold();
                    column.Item().Text(model.Description);

                    column.Item().Text($"{t["injuryType"]}: {model.InjuryType}");

                    if (!string.IsNullOrWhiteSpace(model.FirstAidGiven))
                        column.Item().Text($"{t["firstAidGiven"]}: {model.FirstAidGiven}");

                    column.Item().Text($"{t["doctorCalled"]}: {(model.DoctorCalled ? t["yes"] : t["no"])}");
                    if (!string.IsNullOrWhiteSpace(model.DoctorNotes))
                        column.Item().Text($"{t["doctorNotes"]}: {model.DoctorNotes}");

                    column.Item().Text($"{t["parentNotified"]}: {(model.ParentNotified ? t["yes"] : t["no"])}");
                    if (model.ParentNotifiedAt is not null)
                        column.Item().Text($"{t["parentNotifiedAt"]}: {model.ParentNotifiedAt:yyyy-MM-dd HH:mm}");
                    if (!string.IsNullOrWhiteSpace(model.ParentNotifiedHow))
                        column.Item().Text($"{t["parentNotifiedHow"]}: {model.ParentNotifiedHow}");

                    if (!string.IsNullOrWhiteSpace(model.Witnesses))
                        column.Item().Text($"{t["witnesses"]}: {model.Witnesses}");

                    if (!string.IsNullOrWhiteSpace(model.FollowUp))
                        column.Item().Text($"{t["followUp"]}: {model.FollowUp}");

                    column.Item().PaddingTop(30).Text($"{t["signature"]}: _______________________");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
