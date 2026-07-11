namespace ChildCare.Domain.Enums;

// All six values are single words, so a plain lowercase round-trips fine (unlike
// ChildEventTypeExtensions' snake_case mapping) — this just centralizes the conversion so the
// EF Core column conversion, the API request parser, and the response mapper never drift onto
// three independent copies of the same lowercase logic.
public static class GroupActivityTypeExtensions
{
    public static string ToWireString(this GroupActivityType type) => type.ToString().ToLowerInvariant();

    public static bool TryParseWireString(string value, out GroupActivityType type) =>
        Enum.TryParse(value, ignoreCase: true, out type);
}
