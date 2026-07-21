namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the contract signing-invitation email (feature 024-esignature, FR-003),
/// same Labels-dictionary idiom as TourInvitationEmailLabels/EnrollmentEmailLabels.
/// </summary>
internal record ContractSigningLabels(
    string Subject,
    string TitleFormat,
    string BodyFormat,
    string SignButton);

internal static class ContractSigningEmailLabels
{
    private static readonly Dictionary<string, ContractSigningLabels> Labels = new()
    {
        ["nl"] = new ContractSigningLabels(
            Subject: "Onderteken de opvangovereenkomst",
            TitleFormat: "Overeenkomst voor {0}",
            BodyFormat: "De opvangovereenkomst voor {0} bij {1} staat klaar om digitaal te ondertekenen. De link hieronder is 72 uur geldig.",
            SignButton: "Overeenkomst bekijken en ondertekenen"),
        ["fr"] = new ContractSigningLabels(
            Subject: "Signez le contrat d'accueil",
            TitleFormat: "Contrat pour {0}",
            BodyFormat: "Le contrat d'accueil pour {0} chez {1} est prêt à être signé numériquement. Le lien ci-dessous est valable 72 heures.",
            SignButton: "Voir et signer le contrat"),
        ["en"] = new ContractSigningLabels(
            Subject: "Sign the enrolment contract",
            TitleFormat: "Contract for {0}",
            BodyFormat: "The enrolment contract for {0} at {1} is ready to sign digitally. The link below is valid for 72 hours.",
            SignButton: "Review and sign the contract"),
    };

    public static ContractSigningLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
