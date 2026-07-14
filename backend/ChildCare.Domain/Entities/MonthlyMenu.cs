namespace ChildCare.Domain.Entities;

// monthly_menus (data-model.md, feature 013e) — one row per location per year/month.
// PublishedAt is the state: null = draft (not parent-visible, FR-002), set = published (FR-003).
// Un-publish (FR-004) clears it back to null — no separate status enum, mirroring DayReservation's
// use of a nullable timestamp as implicit state.
public class MonthlyMenu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LocationId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MonthlyMenuDay> Days { get; set; } = [];
}
