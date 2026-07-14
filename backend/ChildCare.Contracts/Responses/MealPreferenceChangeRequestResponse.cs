namespace ChildCare.Contracts.Responses;

// Feature 013e — contracts/monthly-menu-api.md.

// FR-013 — the child's currently-active health records shown alongside a request for context.
public record MealPreferenceRequestHealthRecordEntry(
    Guid Id,
    string RecordType,
    string Title,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

public record MealPreferenceChangeRequestResponse(
    Guid Id,
    Guid ChildId,
    string ChildName,
    string RequestedByName,
    string? NewTexture,
    List<string>? NewDietaryType,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    string? DecisionNotes,
    IReadOnlyList<MealPreferenceRequestHealthRecordEntry> ActiveHealthRecords);

// Parent-facing current preference read. texture/dietaryType null = no MealPreference row yet
// ("Geen voorkeur"). hasPendingRequest true → the parent app disables/relabels "Voorkeur aanpassen".
public record ParentMealPreferenceResponse(
    string? Texture,
    List<string>? DietaryType,
    bool HasPendingRequest);
