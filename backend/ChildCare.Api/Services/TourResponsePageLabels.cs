namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the tour-invitation accept/decline landing page (feature 023) — mirrors
/// UnsubscribePageLabels' framework-minimal precedent exactly (no design-system chrome, just
/// legible text and a clear confirmation).
/// </summary>
internal record TourResponsePageLabels(
    string AcceptedTitle,
    string AcceptedText,
    string DeclinedTitle,
    string DeclinedText,
    string NoLongerActiveTitle,
    string NoLongerActiveText,
    string InvalidLinkText);

internal static class TourResponsePageLabelsProvider
{
    private static readonly Dictionary<string, TourResponsePageLabels> Labels = new()
    {
        ["nl"] = new TourResponsePageLabels(
            AcceptedTitle: "Tot dan!",
            AcceptedText: "Bedankt om te laten weten dat je erbij bent voor {0}.",
            DeclinedTitle: "Geen probleem",
            DeclinedText: "Bedankt om te laten weten dat dit moment niet past voor {0}.",
            NoLongerActiveTitle: "Deze uitnodiging is niet meer actief",
            NoLongerActiveText: "Neem gerust rechtstreeks contact met ons op.",
            InvalidLinkText: "Deze link is niet (meer) geldig."),
        ["fr"] = new TourResponsePageLabels(
            AcceptedTitle: "À bientôt !",
            AcceptedText: "Merci de nous avoir confirmé votre présence pour {0}.",
            DeclinedTitle: "Pas de souci",
            DeclinedText: "Merci de nous avoir informés que ce moment ne convient pas pour {0}.",
            NoLongerActiveTitle: "Cette invitation n'est plus active",
            NoLongerActiveText: "N'hésitez pas à nous contacter directement.",
            InvalidLinkText: "Ce lien n'est pas (plus) valide."),
        ["en"] = new TourResponsePageLabels(
            AcceptedTitle: "See you then!",
            AcceptedText: "Thanks for confirming you'll be there for {0}.",
            DeclinedTitle: "No problem",
            DeclinedText: "Thanks for letting us know this time doesn't work for {0}.",
            NoLongerActiveTitle: "This invitation is no longer active",
            NoLongerActiveText: "Feel free to contact us directly.",
            InvalidLinkText: "This link isn't valid."),
    };

    public static TourResponsePageLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
