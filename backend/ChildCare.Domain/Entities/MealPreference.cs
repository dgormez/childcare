using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// child_meal_preferences (data-model.md, feature 013d) — one row per child (unique ChildId).
// Independent of, and complementary to, Child.DietaryRestrictions (free-text, feature 006).
public class MealPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    public MealTexture Texture { get; set; } = MealTexture.Normal;
    public List<DietaryType> DietaryType { get; set; } = [];
    public MealPortionSize PortionSize { get; set; } = MealPortionSize.Normal;
    public string? AdditionalNotes { get; set; }

    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
