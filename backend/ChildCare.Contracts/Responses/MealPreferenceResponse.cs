namespace ChildCare.Contracts.Responses;

public record MealPreferenceResponse(
    Guid ChildId,
    string Texture,
    List<string> DietaryType,
    string PortionSize,
    string? AdditionalNotes,
    Guid? UpdatedBy,
    DateTime? UpdatedAt);
