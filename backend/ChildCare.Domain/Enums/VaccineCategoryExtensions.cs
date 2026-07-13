namespace ChildCare.Domain.Enums;

// Wire values are snake_case ("aanbevolen_niet_gratis") — mirrors HealthRecordTypeExtensions's
// rationale: a plain ToString().ToLowerInvariant() would produce "aanbevolennietgratis" (no
// underscore), so explicit mapping is needed everywhere this enum crosses a string boundary.
public static class VaccineCategoryExtensions
{
    public static string ToWireString(this VaccineCategory category) => category switch
    {
        VaccineCategory.AanbevolenNietGratis => "aanbevolen_niet_gratis",
        _ => category.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out VaccineCategory category)
    {
        switch (value)
        {
            case "aanbevolen_niet_gratis":
                category = VaccineCategory.AanbevolenNietGratis;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out category);
        }
    }
}
