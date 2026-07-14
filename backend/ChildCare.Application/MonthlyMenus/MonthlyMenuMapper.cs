using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.MonthlyMenus;

public enum MonthlyMenuFailure
{
    NotFound,
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
        IsPublished: menu.PublishedAt is not null,
        PublishedAt: menu.PublishedAt,
        Days: menu.Days
            .OrderBy(d => d.MenuDate)
            .Select(ToDayEntry)
            .ToList());

    public static MonthlyMenuResponse EmptyShell() => new(
        Exists: false,
        IsPublished: false,
        PublishedAt: null,
        Days: []);

    public static MonthlyMenuDayEntry ToDayEntry(MonthlyMenuDay day) =>
        new(day.MenuDate, day.Soup, day.MainCourse, day.Dessert, day.Notes);
}
