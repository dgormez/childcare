namespace ChildCare.Contracts.Responses;

// Feature 013e — contracts/monthly-menu-api.md. Variant added feature 013j —
// contracts/013j-monthly-menu-variants/monthly-menu-variants-api.md.

public record MonthlyMenuDayEntry(
    DateOnly Date,
    string? Soup,
    string? MainCourse,
    string? Dessert,
    string? Notes);

// Director authoring read — returns exists:false shell when no menu row exists yet so the web
// form can render blank inputs. Variant: null = base menu, otherwise the wire-string DietaryType
// this response is for (echoes back what was requested, for client-side confirmation).
public record MonthlyMenuResponse(
    bool Exists,
    string? Variant,
    bool IsPublished,
    DateTime? PublishedAt,
    IReadOnlyList<MonthlyMenuDayEntry> Days);

// Publish / un-publish response.
public record MonthlyMenuPublishStateResponse(
    bool IsPublished,
    DateTime? PublishedAt);

// Parent-facing per-(location, child) entry (013j — was per-location only in 013e/research.md
// R4/R5; restructured since variant resolution is inherently per-child). isPublished:false → the
// client renders the "Menu nog niet beschikbaar" placeholder instead of an empty grid.
// ResolvedVariant: null means the base menu was resolved for this child — the client never sees
// the "base" DB sentinel, only null or a real DietaryType wire string (FR-011: this field exists
// for the client's own labeling logic, not as user-visible "fallback" messaging).
public record ParentMonthlyMenuEntry(
    Guid LocationId,
    string LocationName,
    Guid ChildId,
    string ChildName,
    string? ResolvedVariant,
    bool IsPublished,
    IReadOnlyList<MonthlyMenuDayEntry> Days,
    IReadOnlyList<DateOnly> ClosureDates);
