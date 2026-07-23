namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the platform-admin organisation-invitation email (feature 032, FR-003),
/// same Labels-dictionary idiom as TourInvitationEmailLabels/ContractSigningEmailLabels — fixed
/// pairing, `"nl"` fallback for an unrecognized locale.
/// </summary>
internal record OrganisationInvitationLabels(
    string Subject,
    string Title,
    string BodyFormat,
    string BodyNoNoteFormat,
    string RegisterButton);

internal static class OrganisationInvitationEmailLabels
{
    private static readonly Dictionary<string, OrganisationInvitationLabels> Labels = new()
    {
        ["nl"] = new OrganisationInvitationLabels(
            Subject: "Uitnodiging voor ChildCare",
            Title: "Je bent uitgenodigd",
            BodyFormat: "Je bent uitgenodigd om je kinderopvangorganisatie \"{0}\" te registreren op ChildCare. Klik hieronder om je account aan te maken.",
            BodyNoNoteFormat: "Je bent uitgenodigd om je kinderopvangorganisatie te registreren op ChildCare. Klik hieronder om je account aan te maken.",
            RegisterButton: "Organisatie registreren"),
        ["fr"] = new OrganisationInvitationLabels(
            Subject: "Invitation à ChildCare",
            Title: "Vous êtes invité(e)",
            BodyFormat: "Vous êtes invité(e) à inscrire votre organisation d'accueil d'enfants \"{0}\" sur ChildCare. Cliquez ci-dessous pour créer votre compte.",
            BodyNoNoteFormat: "Vous êtes invité(e) à inscrire votre organisation d'accueil d'enfants sur ChildCare. Cliquez ci-dessous pour créer votre compte.",
            RegisterButton: "Inscrire l'organisation"),
        ["en"] = new OrganisationInvitationLabels(
            Subject: "Invitation to ChildCare",
            Title: "You've been invited",
            BodyFormat: "You've been invited to register your childcare organisation \"{0}\" on ChildCare. Click below to create your account.",
            BodyNoNoteFormat: "You've been invited to register your childcare organisation on ChildCare. Click below to create your account.",
            RegisterButton: "Register organisation"),
    };

    public static OrganisationInvitationLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
