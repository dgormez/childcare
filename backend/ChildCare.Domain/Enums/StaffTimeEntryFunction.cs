namespace ChildCare.Domain.Enums;

// Medewerkersbeleid subsidy function categories (spec.md FR-004) — distinct from
// QualificationLevel (training level, used for BKR ratio calc); no existing enum maps to these
// categories (research.md R1). Wire strings are each a single word, so the default
// ToString().ToLowerInvariant() round-trips correctly, but explicit extensions are kept anyway
// for consistency with every other enum that crosses a string boundary in this codebase (e.g.
// ChildEventTypeExtensions) and to keep the wire contract explicit rather than implicit.
public enum StaffTimeEntryFunction
{
    Kinderbegeleider,
    Logistiek,
    Verantwoordelijke,
}

public static class StaffTimeEntryFunctionExtensions
{
    public static string ToWireString(this StaffTimeEntryFunction function) =>
        function.ToString().ToLowerInvariant();

    public static bool TryParseWireString(string value, out StaffTimeEntryFunction function) =>
        Enum.TryParse(value, ignoreCase: true, out function);
}
