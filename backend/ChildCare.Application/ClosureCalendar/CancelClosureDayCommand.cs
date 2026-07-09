using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record CancelClosureDayCommand(Guid Id, Guid CancelledBy) : IRequest<CancelClosureCalendarResult>;

public class CancelClosureDayCommandHandler(
    ITenantDbContext db,
    ClosureAttendanceService attendance,
    ClosureNotificationService notifications)
    : IRequestHandler<CancelClosureDayCommand, CancelClosureCalendarResult>
{
    public async Task<CancelClosureCalendarResult> Handle(CancelClosureDayCommand request, CancellationToken cancellationToken)
    {
        var closure = await db.KdvClosureDays.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (closure is null)
            return CancelClosureCalendarResult.Fail(ClosureCalendarFailure.NotFound);

        if (closure.Status == ClosureStatus.Draft)
        {
            db.KdvClosureDays.Remove(closure);
            await db.SaveChangesAsync(cancellationToken);
            return CancelClosureCalendarResult.DraftRemoved();
        }

        if (closure.Status != ClosureStatus.Published)
            return CancelClosureCalendarResult.Fail(ClosureCalendarFailure.NotEditable);

        var attendanceSummary = await attendance.ReleaseClosureAsync(closure, cancellationToken);

        closure.Status = ClosureStatus.Cancelled;
        closure.CancelledAt = DateTime.UtcNow;
        closure.CancelledBy = request.CancelledBy;
        closure.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var notificationSummary = closure.NotifyParents && closure.NotificationSentAt is not null
            ? await notifications.NotifyAsync(closure, ClosureNotificationKind.Cancelled, cancellationToken)
            : new ClosureNotificationSummary(0, 0, 0, 0);

        var response = new CancelClosureDayResponse(
            ClosureCalendarMapper.ToResponse(closure),
            attendanceSummary.Released,
            attendanceSummary.Preserved,
            new ClosureNotificationSummaryResponse(
                notificationSummary.Recipients,
                notificationSummary.PushSent,
                notificationSummary.PushFailed,
                notificationSummary.MessagesCreated));

        return CancelClosureCalendarResult.Success(response);
    }
}
