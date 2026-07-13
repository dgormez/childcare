namespace ChildCare.Contracts.Requests;

// All fields optional/nullable — omitted (null) means "no change" on update, mirrors
// CorrectAttendanceRecordCommand's null-coalesce merge pattern. On first creation, an omitted
// field falls back to its column default (normal/[]/normal/null).
public record UpsertMealPreferenceRequest(
    string? Texture,
    List<string>? DietaryType,
    string? PortionSize,
    string? AdditionalNotes);
