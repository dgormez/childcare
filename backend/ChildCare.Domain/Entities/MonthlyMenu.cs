namespace ChildCare.Domain.Entities;

// monthly_menus (data-model.md, feature 013e; Variant added feature 013j) — one row per
// location per year/month per variant. PublishedAt is the state: null = draft (not
// parent-visible, FR-002), set = published (FR-003). Un-publish (FR-004) clears it back to null
// — no separate status enum, mirroring DayReservation's use of a nullable timestamp as implicit
// state.
public class MonthlyMenu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LocationId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    // Feature 013j — "base" = the base menu; a DietaryType wire string = that variant. Deliberately
    // a plain non-nullable string, not DietaryType? — two reasons: (1) Postgres unique indexes
    // treat NULL as distinct from every other NULL, which would silently allow more than one
    // base-menu row per location/year/month (research.md); (2) an EF Core HasConversion for
    // DietaryType? on this property collides with MealPreference.DietaryType's pre-existing
    // List<DietaryType> converter in a Npgsql.EntityFrameworkCore.PostgreSQL array-conversion
    // provider bug (research.md's "MonthlyMenu.Variant storage" decision) — parsing to/from
    // DietaryType happens in the Application layer (MonthlyMenuVariantHelper), not via EF.
    public string Variant { get; set; } = "base";

    public DateTime? PublishedAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MonthlyMenuDay> Days { get; set; } = [];
}
