namespace ChildCare.Domain.Entities;

// monthly_menu_days (data-model.md, feature 013e) — one row per calendar date within a MonthlyMenu.
// A date with no row, or a row with all course fields null, both render as "—" (FR-007).
public class MonthlyMenuDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuId { get; set; }
    public DateOnly MenuDate { get; set; }

    public string? LunchMeal            { get; set; }
    public string? AlternativeLunchMeal  { get; set; }
    // 3pm snack (FR: collation, NL: tussendoortje).
    public string? Snack                { get; set; }
    public string? Notes                { get; set; }
}
