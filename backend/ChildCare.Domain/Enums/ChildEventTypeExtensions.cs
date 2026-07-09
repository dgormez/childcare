namespace ChildCare.Domain.Enums;

// Event-type wire values are snake_case (spec.md's literal naming: "feeding_bottle",
// "feeding_solid"), unlike every other enum in this codebase (e.g. ContractStatus), which is a
// single word and round-trips fine through a plain ToString().ToLowerInvariant(). A generic
// lowercase conversion silently mismatches these two multi-word values, so explicit mapping is
// needed everywhere ChildEventType crosses a string boundary (DB column, API request/response).
public static class ChildEventTypeExtensions
{
    public static string ToWireString(this ChildEventType type) => type switch
    {
        ChildEventType.FeedingBottle => "feeding_bottle",
        ChildEventType.FeedingSolid => "feeding_solid",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out ChildEventType type)
    {
        switch (value)
        {
            case "feeding_bottle":
                type = ChildEventType.FeedingBottle;
                return true;
            case "feeding_solid":
                type = ChildEventType.FeedingSolid;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out type);
        }
    }
}
