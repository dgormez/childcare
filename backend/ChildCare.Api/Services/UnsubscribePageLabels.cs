namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the digest unsubscribe/resubscribe page (feature 020) — the one
/// parent-facing web surface this feature adds outside of email itself. Framework-minimal per
/// spec.md's UX Requirements (no design-system chrome, just legible text and a clear action).
/// </summary>
internal record UnsubscribePageLabels(
    string Title,
    string SubscribedText,
    string UnsubscribedText,
    string UnsubscribeButton,
    string ResubscribeButton,
    string InvalidLinkText);

internal static class UnsubscribePageLabelsProvider
{
    private static readonly Dictionary<string, UnsubscribePageLabels> Labels = new()
    {
        ["nl"] = new UnsubscribePageLabels(
            Title: "Dagverslag e-mail",
            SubscribedText: "Je ontvangt momenteel het dagelijkse verslag per e-mail.",
            UnsubscribedText: "Je bent uitgeschreven voor het dagelijkse verslag per e-mail.",
            UnsubscribeButton: "Uitschrijven",
            ResubscribeButton: "Opnieuw inschrijven",
            InvalidLinkText: "Deze link is niet (meer) geldig."),
        ["fr"] = new UnsubscribePageLabels(
            Title: "Rapport quotidien par e-mail",
            SubscribedText: "Vous recevez actuellement le rapport quotidien par e-mail.",
            UnsubscribedText: "Vous êtes désabonné(e) du rapport quotidien par e-mail.",
            UnsubscribeButton: "Se désabonner",
            ResubscribeButton: "Se réabonner",
            InvalidLinkText: "Ce lien n'est pas (plus) valide."),
        ["en"] = new UnsubscribePageLabels(
            Title: "Daily report email",
            SubscribedText: "You're currently receiving the daily report by email.",
            UnsubscribedText: "You've unsubscribed from the daily report email.",
            UnsubscribeButton: "Unsubscribe",
            ResubscribeButton: "Resubscribe",
            InvalidLinkText: "This link isn't valid."),
    };

    public static UnsubscribePageLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
