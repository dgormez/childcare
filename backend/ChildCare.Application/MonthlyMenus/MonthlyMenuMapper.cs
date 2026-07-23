using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.MonthlyMenus;

public enum MonthlyMenuFailure
{
    NotFound,
    // Feature 013j FR-006 — the requested variant isn't in the location's
    // MenuVariantPriorityOrder, so no author/publish/unpublish write is allowed against it.
    VariantNotEnabled,
}

public class MonthlyMenuPublishResult
{
    public MonthlyMenuPublishStateResponse? Response { get; private init; }
    public MonthlyMenuFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static MonthlyMenuPublishResult Success(MonthlyMenuPublishStateResponse response) => new() { Response = response };
    public static MonthlyMenuPublishResult Fail(MonthlyMenuFailure failure) => new() { Failure = failure };
}

public static class MonthlyMenuMapper
{
    public static MonthlyMenuResponse ToResponse(MonthlyMenu menu) => new(
        Exists: true,
        Variant: menu.Variant == MonthlyMenuVariantHelper.BaseSentinel ? null : menu.Variant,
        IsPublished: menu.PublishedAt is not null,
        PublishedAt: menu.PublishedAt,
        Days: menu.Days
            .OrderBy(d => d.MenuDate)
            .Select(ToDayEntry)
            .ToList());

    public static MonthlyMenuResponse EmptyShell(DietaryType? variant = null) => new(
        Exists: false,
        Variant: variant?.ToWireString(),
        IsPublished: false,
        PublishedAt: null,
        Days: []);

    public static MonthlyMenuDayEntry ToDayEntry(MonthlyMenuDay day) =>
        new(day.MenuDate, day.LunchMeal, day.AlternativeLunchMeal, day.Snack, day.Notes);
}
