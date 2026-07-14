using ChildCare.Domain.Enums;

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

    // Feature 013j — null = the base menu; a real value = that DietaryType's variant. Stored at
    // the DB level as a non-nullable "base" sentinel string (TenantDbContext.cs), never as a
    // nullable column — see specs/013j-monthly-menu-variants/research.md for why: Postgres
    // unique indexes treat NULL as distinct from every other NULL, which would silently allow
    // more than one base-menu row per location/year/month.
    public DietaryType? Variant { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MonthlyMenuDay> Days { get; set; } = [];
}
