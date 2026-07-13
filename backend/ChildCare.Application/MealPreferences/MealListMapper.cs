using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.MealPreferences;

public static class MealListMapper
{
    public static MealListChildEntry ToChildEntry(Child child, MealPreference? preference, bool hasStandingMedication) =>
        new(
            child.Id,
            child.FirstName,
            child.LastName,
            (preference?.Texture ?? MealTexture.Normal).ToString().ToLowerInvariant(),
            (preference?.DietaryType ?? []).Select(d => d.ToWireString()).ToList(),
            (preference?.PortionSize ?? MealPortionSize.Normal).ToString().ToLowerInvariant(),
            preference?.AdditionalNotes,
            preference is not null,
            ToAllergySeverityWireString(child.AllergySeverity),
            hasStandingMedication);

    public static MealPreferenceResponse ToPreferenceResponse(MealPreference preference) =>
        new(
            preference.ChildId,
            preference.Texture.ToString().ToLowerInvariant(),
            preference.DietaryType.Select(d => d.ToWireString()).ToList(),
            preference.PortionSize.ToString().ToLowerInvariant(),
            preference.AdditionalNotes,
            preference.UpdatedBy,
            preference.UpdatedAt);

    // research.md R1: Child.AllergySeverity (feature 006) is the sole source for this flag —
    // HealthRecord's Allergy detail rows are not additionally consulted.
    private static string ToAllergySeverityWireString(AllergySeverity? severity) => severity switch
    {
        AllergySeverity.Severe => "severe",
        AllergySeverity.Mild or AllergySeverity.Moderate => "mild_moderate",
        _ => "none",
    };
}
