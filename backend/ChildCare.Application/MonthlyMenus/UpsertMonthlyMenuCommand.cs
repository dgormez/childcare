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
            // Flushed separately from the Add loop below: EF Core doesn't guarantee delete-before-
            // insert ordering for unrelated rows in the same SaveChanges batch, and the new day set
            // can reuse dates from the old one — an unflushed delete+insert of the same
            // (MenuId, MenuDate) pair would trip the unique constraint.
            db.MonthlyMenuDays.RemoveRange(menu.Days);
            menu.Days.Clear();
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var day in request.Days)
        {
            // Explicit Add on the DbSet, not just the navigation collection: MonthlyMenuDay.Id is
            // pre-populated via a Guid.NewGuid() property initializer, and EF Core's graph-based
            // state detection treats a reachable entity with a non-default key as pre-existing
            // ("Modified") rather than new — that produced an UPDATE matching zero rows instead of
            // an INSERT. An explicit Add forces the correct Added state regardless of the key value.
            var newDay = new MonthlyMenuDay
            {
                MenuId = menu.Id,
                MenuDate = day.Date,
                Soup = NullIfBlank(day.Soup),
                MainCourse = NullIfBlank(day.MainCourse),
                Dessert = NullIfBlank(day.Dessert),
                Notes = NullIfBlank(day.Notes),
            };
            // Add on the DbSet alone: EF's relationship fixup adds it into menu.Days automatically
            // (MenuId matches the tracked parent), so also calling menu.Days.Add(newDay) here would
            // insert it into that List a second time.
            db.MonthlyMenuDays.Add(newDay);
        }

        await db.SaveChangesAsync(cancellationToken);

        return MonthlyMenuMapper.ToResponse(menu);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
