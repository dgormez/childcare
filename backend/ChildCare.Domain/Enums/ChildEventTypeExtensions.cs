namespace ChildCare.Domain.Enums;

// Event-type wire values are snake_case (spec.md's literal naming: "feeding_bottle",
// "feeding_solid", "growth_check"), unlike every other enum in this codebase (e.g.
// ContractStatus), which is a single word and round-trips fine through a plain
// ToString().ToLowerInvariant(). A generic lowercase conversion silently mismatches these
// multi-word values, so explicit mapping is needed everywhere ChildEventType crosses a string
// boundary (DB column, API request/response). "measurement" (feature 009a-child-events-
// custom-type) is deliberately absent from TryParseWireString — the rename is a hard cutover
// (spec.md FR-008), not an ongoing dual-write alias; every tenant schema's existing
// `measurement` rows must be backfilled to `growth_check` (the `backfill-growth-check` CLI
// command) before a build without this mapping is deployed, or reads on any un-migrated row
// will throw (research.md R2).
public static class ChildEventTypeExtensions
{
    public static string ToWireString(this ChildEventType type) => type switch
    {
        ChildEventType.FeedingBottle => "feeding_bottle",
        ChildEventType.FeedingSolid => "feeding_solid",
        ChildEventType.GrowthCheck => "growth_check",
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
            case "growth_check":
                type = ChildEventType.GrowthCheck;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out type);
        }
    }
}
