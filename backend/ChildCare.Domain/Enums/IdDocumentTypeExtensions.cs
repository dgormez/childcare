namespace ChildCare.Domain.Enums;

// Wire values are snake_case ("birth_certificate", "kids_id") — mirrors ChildEventTypeExtensions'/
// MilestoneObservationStatusExtensions' rationale: a plain ToString().ToLowerInvariant() would
// produce "birthcertificate"/"kidsid" (no underscore), so explicit mapping is needed everywhere
// this enum crosses a string boundary (spec.md FR-001).
public static class IdDocumentTypeExtensions
{
    public static string ToWireString(this IdDocumentType type) => type switch
    {
        IdDocumentType.BirthCertificate => "birth_certificate",
        IdDocumentType.KidsId => "kids_id",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string? value, out IdDocumentType type)
    {
        switch (value)
        {
            case "birth_certificate":
                type = IdDocumentType.BirthCertificate;
                return true;
            case "kids_id":
                type = IdDocumentType.KidsId;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out type);
        }
    }
}
