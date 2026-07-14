namespace ChildCare.Contracts.Requests;

// Feature 013e — contracts/monthly-menu-api.md. PUT replaces the full days array for the
// location/year/month (whole-month replace on write, mirrors 013d's UpsertMealPreference shape).
public record UpsertMonthlyMenuDayRequest(
    DateOnly Date,
    string? Soup,
    string? MainCourse,
    string? Dessert,
    string? Notes);

public record UpsertMonthlyMenuRequest(
    List<UpsertMonthlyMenuDayRequest> Days);
