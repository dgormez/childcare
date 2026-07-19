namespace ChildCare.Api.Services;

/// <summary>
/// NL/FR/EN copy for the daily-report digest email (feature 020, FR-005/FR-014), same
/// Labels-dictionary idiom as SendAnnouncementCommandHandler/ClosureNotificationService/
/// PaymentReminderNotificationService use for push copy — fixed pairing, `"nl"` fallback for an
/// unrecognized locale (spec.md Technical Requirements).
/// </summary>
internal record DailyReportLabels(
    string Subject,
    string TitleFormat,
    string NapsLabel,
    string BottlesLabel,
    string DiapersLabel,
    string MoodLabel,
    string TemperatureLabel,
    string MedicationAdministeredText,
    string ActivitiesLabel,
    string GroupActivitiesLabel,
    string NoUpdatesText,
    string UnsubscribeText);

internal static class DailyReportEmailLabels
{
    private static readonly Dictionary<string, DailyReportLabels> Labels = new()
    {
        ["nl"] = new DailyReportLabels(
            Subject: "Dagverslag van {0}",
            TitleFormat: "De dag van {0}",
            NapsLabel: "Dutjes",
            BottlesLabel: "Flesjes",
            DiapersLabel: "Luierwissels",
            MoodLabel: "Stemming",
            TemperatureLabel: "Temperatuur",
            MedicationAdministeredText: "Medicatie werd vandaag toegediend.",
            ActivitiesLabel: "Activiteiten",
            GroupActivitiesLabel: "Groepsactiviteiten",
            NoUpdatesText: "Er zijn vandaag nog geen updates geregistreerd.",
            UnsubscribeText: "Uitschrijven voor het dagverslag"),
        ["fr"] = new DailyReportLabels(
            Subject: "Rapport quotidien de {0}",
            TitleFormat: "La journée de {0}",
            NapsLabel: "Siestes",
            BottlesLabel: "Biberons",
            DiapersLabel: "Changes",
            MoodLabel: "Humeur",
            TemperatureLabel: "Température",
            MedicationAdministeredText: "Un médicament a été administré aujourd'hui.",
            ActivitiesLabel: "Activités",
            GroupActivitiesLabel: "Activités de groupe",
            NoUpdatesText: "Aucune mise à jour n'a encore été enregistrée aujourd'hui.",
            UnsubscribeText: "Se désabonner du rapport quotidien"),
        ["en"] = new DailyReportLabels(
            Subject: "{0}'s daily report",
            TitleFormat: "{0}'s day",
            NapsLabel: "Naps",
            BottlesLabel: "Bottles",
            DiapersLabel: "Diaper changes",
            MoodLabel: "Mood",
            TemperatureLabel: "Temperature",
            MedicationAdministeredText: "Medication was administered today.",
            ActivitiesLabel: "Activities",
            GroupActivitiesLabel: "Group activities",
            NoUpdatesText: "No updates logged today.",
            UnsubscribeText: "Unsubscribe from the daily report"),
    };

    public static DailyReportLabels For(string locale) =>
        Labels.TryGetValue(locale, out var found) ? found : Labels["nl"];
}
