using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

public record MarkAbsenceCommand(Guid Id, bool IsAbsent, string? AbsenceReason) : IRequest<StaffScheduleResult>;

public class MarkAbsenceCommandValidator : AbstractValidator<MarkAbsenceCommand>
{
    public MarkAbsenceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        // FR-005: a reason is required iff IsAbsent is true.
        RuleFor(x => x.AbsenceReason)
            .Must(v => StaffScheduleMapper.TryParseAbsenceReason(v, out _))
            .When(x => x.IsAbsent);
    }
}

// FR-005/FR-006: mark/un-mark an entry absent. Future-dated only (FR-004) — absence-marking is
// an edit like any other. This never touches feature 010's live BKR path (research.md R1); it
// only affects GetProjectedOnDutyQuery's planning-only count.
public class MarkAbsenceCommandHandler(ITenantDbContext db) : IRequestHandler<MarkAbsenceCommand, StaffScheduleResult>
{
    public async Task<StaffScheduleResult> Handle(MarkAbsenceCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffSchedules.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (entry is null)
            return StaffScheduleResult.Fail(StaffScheduleFailure.NotFound);

        if (entry.Date < BelgianCalendarDay.Today())
            return StaffScheduleResult.Fail(StaffScheduleFailure.PastDate);

        if (request.IsAbsent)
        {
            StaffScheduleMapper.TryParseAbsenceReason(request.AbsenceReason, out var reason);
            entry.IsAbsent = true;
            entry.AbsenceReason = reason;
        }
        else
        {
            entry.IsAbsent = false;
            entry.AbsenceReason = null;
        }

        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return StaffScheduleResult.Success(StaffScheduleMapper.ToResponse(entry));
    }
}
