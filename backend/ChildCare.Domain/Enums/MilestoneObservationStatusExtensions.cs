namespace ChildCare.Domain.Enums;

// Wire values are snake_case ("not_yet") — mirrors VaccineCategoryExtensions'/
// HealthRecordTypeExtensions' rationale: a plain ToString().ToLowerInvariant() would produce
// "notyet" (no underscore), so explicit mapping is needed everywhere this enum crosses a string
// boundary (spec.md FR-002/FR-012).
public static class MilestoneObservationStatusExtensions
{
    public static string ToWireString(this MilestoneObservationStatus status) => status switch
    {
        MilestoneObservationStatus.NotYet => "not_yet",
        _ => status.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out MilestoneObservationStatus status)
    {
        switch (value)
        {
            case "not_yet":
                status = MilestoneObservationStatus.NotYet;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out status);
        }
    }
}
