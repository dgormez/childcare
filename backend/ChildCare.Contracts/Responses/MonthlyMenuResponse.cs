namespace ChildCare.Contracts.Responses;

// Feature 013e — contracts/monthly-menu-api.md.

public record MonthlyMenuDayEntry(
    DateOnly Date,
    string? Soup,
    string? MainCourse,
    string? Dessert,
    string? Notes);

// Director authoring read — returns exists:false shell when no menu row exists yet so the web
// form can render blank inputs.
public record MonthlyMenuResponse(
    bool Exists,
    bool IsPublished,
    DateTime? PublishedAt,
    IReadOnlyList<MonthlyMenuDayEntry> Days);

// Publish / un-publish response.
public record MonthlyMenuPublishStateResponse(
    bool IsPublished,
    DateTime? PublishedAt);

// Parent-facing per-location entry (research.md R4/R5). isPublished:false → the client renders the
// "Menu nog niet beschikbaar" placeholder instead of an empty grid.
public record ParentMonthlyMenuEntry(
    Guid LocationId,
    string LocationName,
    bool IsPublished,
    IReadOnlyList<MonthlyMenuDayEntry> Days,
    IReadOnlyList<DateOnly> ClosureDates);
