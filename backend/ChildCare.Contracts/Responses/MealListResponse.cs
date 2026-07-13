namespace ChildCare.Contracts.Responses;

public record MealListChildEntry(
    Guid ChildId,
    string FirstName,
    string LastName,
    string Texture,
    List<string> DietaryType,
    string PortionSize,
    string? AdditionalNotes,
    // FR-005: false means "no child_meal_preferences row exists" — the client renders
    // "Geen voorkeur" regardless of the (default) values above, rather than treating this
    // child as if a director had actually chosen Normal/Normal/[]/null.
    bool HasPreference,
    // severe | mild_moderate | none (FR-006, research.md R1)
    string AllergySeverity,
    bool HasStandingMedication);

public record MealListGroupEntry(
    Guid GroupId,
    string GroupName,
    IReadOnlyList<MealListChildEntry> Children);

public record MealListExpectedEntry(IReadOnlyList<MealListChildEntry> Children);

public record MealListResponse(
    DateOnly Date,
    IReadOnlyList<MealListGroupEntry> Groups,
    // Null unless the caller passed includeExpected=true (FR-009).
    MealListExpectedEntry? Expected);
