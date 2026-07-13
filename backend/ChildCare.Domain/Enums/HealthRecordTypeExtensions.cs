namespace ChildCare.Domain.Enums;

// Wire values are snake_case ("chronic_condition", "medication_standing") — mirrors
// ChildEventTypeExtensions's rationale: a plain ToString().ToLowerInvariant() silently drops
// the underscore on multi-word values, so explicit mapping is needed everywhere this enum
// crosses a string boundary (DB column, API request/response).
public static class HealthRecordTypeExtensions
{
    public static string ToWireString(this HealthRecordType type) => type switch
    {
        HealthRecordType.ChronicCondition => "chronic_condition",
        HealthRecordType.MedicationStanding => "medication_standing",
        HealthRecordType.DoctorNote => "doctor_note",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out HealthRecordType type)
    {
        switch (value)
        {
            case "chronic_condition":
                type = HealthRecordType.ChronicCondition;
                return true;
            case "medication_standing":
                type = HealthRecordType.MedicationStanding;
                return true;
            case "doctor_note":
                type = HealthRecordType.DoctorNote;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out type);
        }
    }
}
