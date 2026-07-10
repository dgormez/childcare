using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record UpdateClosureDayCommand(Guid Id, string Label, string ClosureType, bool NotifyParents, Guid UpdatedBy) : IRequest<ClosureCalendarResult>;

public class UpdateClosureDayCommandValidator : AbstractValidator<UpdateClosureDayCommand>
{
    public UpdateClosureDayCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClosureType).NotEmpty().Must(v => ClosureCalendarMapper.TryParseClosureType(v, out _));
        RuleFor(x => x.UpdatedBy).NotEmpty();
    }
}

public class UpdateClosureDayCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateClosureDayCommand, ClosureCalendarResult>
{
    public async Task<ClosureCalendarResult> Handle(UpdateClosureDayCommand request, CancellationToken cancellationToken)
    {
        var closure = await db.KdvClosureDays.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (closure is null)
            return ClosureCalendarResult.Fail(ClosureCalendarFailure.NotFound);
        if (closure.Status != ClosureStatus.Draft)
            return ClosureCalendarResult.Fail(ClosureCalendarFailure.NotEditable);

        ClosureCalendarMapper.TryParseClosureType(request.ClosureType, out var type);
        closure.Label = request.Label.Trim();
        closure.ClosureType = type;
        closure.NotifyParents = request.NotifyParents;
        closure.UpdatedBy = request.UpdatedBy;
        closure.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ClosureCalendarResult.Success(ClosureCalendarMapper.ToResponse(closure));
    }
}
