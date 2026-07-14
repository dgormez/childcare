using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Find-or-create the MonthlyMenu row (as a draft) for LocationId/Year/Month, then replace the
// full MonthlyMenuDay set (whole-month replace on write, contracts/monthly-menu-api.md). Never
// changes publish state — publish/unpublish are separate commands so a draft correction to a
// published menu doesn't accidentally unpublish it (FR-001, FR-005).
public record UpsertMonthlyMenuCommand(
    Guid LocationId,
    int Year,
    int Month,
    List<UpsertMonthlyMenuDayRequest> Days,
    Guid UpdatedBy) : IRequest<MonthlyMenuResponse>;

public class UpsertMonthlyMenuCommandValidator : AbstractValidator<UpsertMonthlyMenuCommand>
{
    public UpsertMonthlyMenuCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("errors.monthly_menu.invalid_month");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).WithMessage("errors.monthly_menu.invalid_year");

        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.Soup).MaximumLength(500).WithMessage("errors.monthly_menu.field_too_long");
            day.RuleFor(d => d.MainCourse).MaximumLength(500).WithMessage("errors.monthly_menu.field_too_long");
            day.RuleFor(d => d.Dessert).MaximumLength(500).WithMessage("errors.monthly_menu.field_too_long");
            day.RuleFor(d => d.Notes).MaximumLength(500).WithMessage("errors.monthly_menu.field_too_long");
        });

        // Every day's date must fall within the URL's year/month (contracts/monthly-menu-api.md).
        RuleFor(x => x)
            .Must(cmd => cmd.Days.All(d => d.Date.Year == cmd.Year && d.Date.Month == cmd.Month))
            .WithMessage("errors.monthly_menu.date_out_of_range");

        // No duplicate dates within a single submission (the unique index would otherwise reject
        // the whole write with a raw DB error).
        RuleFor(x => x.Days)
            .Must(days => days.Select(d => d.Date).Distinct().Count() == days.Count)
            .WithMessage("errors.monthly_menu.duplicate_date");
    }
}

public class UpsertMonthlyMenuCommandHandler(ITenantDbContext db) : IRequestHandler<UpsertMonthlyMenuCommand, MonthlyMenuResponse>
{
    public async Task<MonthlyMenuResponse> Handle(UpsertMonthlyMenuCommand request, CancellationToken cancellationToken)
    {
        var menu = await db.MonthlyMenus
            .Include(m => m.Days)
            .FirstOrDefaultAsync(
                m => m.LocationId == request.LocationId && m.Year == request.Year && m.Month == request.Month,
                cancellationToken);

        if (menu is null)
        {
            menu = new MonthlyMenu
            {
                LocationId = request.LocationId,
                Year = request.Year,
                Month = request.Month,
                CreatedBy = request.UpdatedBy,
            };
            db.MonthlyMenus.Add(menu);
        }
        else
        {
            db.MonthlyMenuDays.RemoveRange(menu.Days);
            menu.Days.Clear();
        }

        foreach (var day in request.Days)
        {
            menu.Days.Add(new MonthlyMenuDay
            {
                MenuDate = day.Date,
                Soup = NullIfBlank(day.Soup),
                MainCourse = NullIfBlank(day.MainCourse),
                Dessert = NullIfBlank(day.Dessert),
                Notes = NullIfBlank(day.Notes),
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return MonthlyMenuMapper.ToResponse(menu);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
