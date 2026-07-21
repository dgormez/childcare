namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the signed-contract-copy email (feature 024-esignature, FR-011). Sent
/// identically to both the parent and the director(s) — no button/link, just the attached PDF
/// (the same idiom TourInvitationEmailLabels/EnrollmentEmailLabels already establish).
/// </summary>
internal record SignedContractLabels(
    string Subject,
    string TitleFormat,
    string BodyFormat);

internal static class SignedContractEmailLabels
{
    private static readonly Dictionary<string, SignedContractLabels> Labels = new()
    {
        ["nl"] = new SignedContractLabels(
            Subject: "Ondertekende overeenkomst",
            TitleFormat: "Overeenkomst voor {0} ondertekend",
            BodyFormat: "De opvangovereenkomst voor {0} is digitaal ondertekend. Je vindt de ondertekende overeenkomst als bijlage bij deze e-mail."),
        ["fr"] = new SignedContractLabels(
            Subject: "Contrat signé",
            TitleFormat: "Contrat pour {0} signé",
            BodyFormat: "Le contrat d'accueil pour {0} a été signé numériquement. Vous trouverez le contrat signé en pièce jointe."),
        ["en"] = new SignedContractLabels(
            Subject: "Signed contract",
            TitleFormat: "Contract for {0} signed",
            BodyFormat: "The enrolment contract for {0} has been digitally signed. You'll find the signed contract attached to this email."),
    };

    public static SignedContractLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
