using System.Data.Common;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record CreateClosureDayCommand(
    Guid LocationId,
    DateOnly Date,
    string Label,
    string ClosureType,
    bool NotifyParents,
    Guid CreatedBy) : IRequest<ClosureCalendarResult>;

public class CreateClosureDayCommandValidator : AbstractValidator<CreateClosureDayCommand>
{
    public CreateClosureDayCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClosureType).Must(v => ClosureCalendarMapper.TryParseClosureType(v, out _));
        RuleFor(x => x.CreatedBy).NotEmpty();
    }
}

public class CreateClosureDayCommandHandler(ITenantDbContext db) : IRequestHandler<CreateClosureDayCommand, ClosureCalendarResult>
{
    public async Task<ClosureCalendarResult> Handle(CreateClosureDayCommand request, CancellationToken cancellationToken)
    {
        if (request.Date < BelgianCalendarDay.Today())
            return ClosureCalendarResult.Fail(ClosureCalendarFailure.PastDate);

        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return ClosureCalendarResult.Fail(ClosureCalendarFailure.LocationNotFound);

        ClosureCalendarMapper.TryParseClosureType(request.ClosureType, out var type);
        var closure = new KdvClosureDay
        {
            LocationId = request.LocationId,
            Date = request.Date,
            Label = request.Label.Trim(),
            ClosureType = type,
            NotifyParents = request.NotifyParents,
            CreatedBy = request.CreatedBy,
        };

        db.KdvClosureDays.Add(closure);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return ClosureCalendarResult.Fail(ClosureCalendarFailure.DuplicateDate);
        }

        return ClosureCalendarResult.Success(ClosureCalendarMapper.ToResponse(closure));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) => ex.InnerException is DbException { SqlState: "23505" };
}
