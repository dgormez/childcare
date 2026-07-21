namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the tour-invitation email (feature 023, FR-015), same Labels-dictionary
/// idiom as DailyReportEmailLabels/EnrollmentEmailLabels — fixed pairing, `"nl"` fallback for an
/// unrecognized locale.
/// </summary>
internal record TourInvitationLabels(
    string Subject,
    string TitleFormat,
    string BodyFormat,
    string AcceptButton,
    string DeclineButton);

internal static class TourInvitationEmailLabels
{
    private static readonly Dictionary<string, TourInvitationLabels> Labels = new()
    {
        ["nl"] = new TourInvitationLabels(
            Subject: "Uitnodiging voor een rondleiding",
            TitleFormat: "Uitnodiging voor {0}",
            BodyFormat: "{1} nodigt je uit voor een rondleiding op {2:dddd d MMMM, HH:mm}. Laat ons weten of dit past.",
            AcceptButton: "Ik kom graag",
            DeclineButton: "Dit past niet"),
        ["fr"] = new TourInvitationLabels(
            Subject: "Invitation à une visite",
            TitleFormat: "Invitation pour {0}",
            BodyFormat: "{1} vous invite à une visite le {2:dddd d MMMM, HH:mm}. Merci de nous indiquer si cela vous convient.",
            AcceptButton: "Je viendrai",
            DeclineButton: "Cela ne convient pas"),
        ["en"] = new TourInvitationLabels(
            Subject: "Tour invitation",
            TitleFormat: "Invitation for {0}",
            BodyFormat: "{1} invites you to a tour on {2:dddd, MMMM d, HH:mm}. Let us know if this works for you.",
            AcceptButton: "I'll be there",
            DeclineButton: "This doesn't work"),
    };

    public static TourInvitationLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
