namespace ChildCare.Domain.Enums;

// Same multi-word wire-mapping note as IncidentInjuryTypeExtensions — InPerson must not round-trip
// through a plain ToString().ToLowerInvariant().
public static class ParentNotifiedHowExtensions
{
    public static string ToWireString(this ParentNotifiedHow value) => value switch
    {
        ParentNotifiedHow.InPerson => "in_person",
        _ => value.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out ParentNotifiedHow parsed)
    {
        if (value == "in_person")
        {
            parsed = ParentNotifiedHow.InPerson;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out parsed);
    }
}
