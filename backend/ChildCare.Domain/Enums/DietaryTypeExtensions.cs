namespace ChildCare.Domain.Enums;

// Wire value is snake_case ("gluten_free") — mirrors HealthRecordTypeExtensions's rationale: a
// plain ToString().ToLowerInvariant() silently drops the underscore on the one multi-word value.
public static class DietaryTypeExtensions
{
    public static string ToWireString(this DietaryType type) => type switch
    {
        DietaryType.GlutenFree => "gluten_free",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out DietaryType type)
    {
        if (value == "gluten_free")
        {
            type = DietaryType.GlutenFree;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out type);
    }
}
