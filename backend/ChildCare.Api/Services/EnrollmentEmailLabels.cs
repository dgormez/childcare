namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the public-enrollment confirmation email (feature 023, FR-009), same
/// Labels-dictionary idiom as DailyReportEmailLabels — fixed pairing, `"nl"` fallback for an
/// unrecognized locale.
/// </summary>
internal record EnrollmentConfirmationLabels(
    string Subject,
    string TitleFormat,
    string BodyFormat,
    string ReferenceLabel);

internal static class EnrollmentEmailLabels
{
    private static readonly Dictionary<string, EnrollmentConfirmationLabels> Labels = new()
    {
        ["nl"] = new EnrollmentConfirmationLabels(
            Subject: "Je aanvraag is ontvangen",
            TitleFormat: "Aanvraag ontvangen voor {0}",
            BodyFormat: "We hebben je aanvraag voor {0} bij {1} ontvangen en toegevoegd aan de wachtlijst. We nemen contact met je op zodra er een plaats beschikbaar is.",
            ReferenceLabel: "Referentienummer"),
        ["fr"] = new EnrollmentConfirmationLabels(
            Subject: "Votre demande a été reçue",
            TitleFormat: "Demande reçue pour {0}",
            BodyFormat: "Nous avons bien reçu votre demande pour {0} auprès de {1} et l'avons ajoutée à la liste d'attente. Nous vous contacterons dès qu'une place sera disponible.",
            ReferenceLabel: "Numéro de référence"),
        ["en"] = new EnrollmentConfirmationLabels(
            Subject: "Your application has been received",
            TitleFormat: "Application received for {0}",
            BodyFormat: "We've received your application for {0} at {1} and added it to the waiting list. We'll be in touch as soon as a place becomes available.",
            ReferenceLabel: "Reference code"),
    };

    public static EnrollmentConfirmationLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
