namespace ChildCare.Domain.Enums;

// Multi-word wire mapping mirroring ChildEventTypeExtensions's convention (feature 009) —
// AllergicReaction must not round-trip through a plain ToString().ToLowerInvariant(), which
// would silently drop the underscore.
public static class IncidentInjuryTypeExtensions
{
    public static string ToWireString(this IncidentInjuryType type) => type switch
    {
        IncidentInjuryType.AllergicReaction => "allergic_reaction",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out IncidentInjuryType type)
    {
        if (value == "allergic_reaction")
        {
            type = IncidentInjuryType.AllergicReaction;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out type);
    }
}
