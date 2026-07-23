namespace ChildCare.Domain.Enums;

// staff_documents.document_type (spec.md FR-011). EmploymentContract's wire string is
// multi-word snake_case ("employment_contract"), unlike the other four values, so it needs
// explicit mapping — the default ToString().ToLowerInvariant() would otherwise produce
// "employmentcontract" (mirrors ChildEventTypeExtensions' handling of its own multi-word values).
public enum StaffDocumentType
{
    EmploymentContract,
    Amendment,
    Qualification,
    Training,
    Other,
}

public static class StaffDocumentTypeExtensions
{
    public static string ToWireString(this StaffDocumentType type) => type switch
    {
        StaffDocumentType.EmploymentContract => "employment_contract",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static bool TryParseWireString(string value, out StaffDocumentType type)
    {
        if (value == "employment_contract")
        {
            type = StaffDocumentType.EmploymentContract;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out type);
    }
}
