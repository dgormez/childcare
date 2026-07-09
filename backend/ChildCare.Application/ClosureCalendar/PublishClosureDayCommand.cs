using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record PublishClosureDayCommand(Guid Id, bool ConfirmExistingAttendance, Guid PublishedBy) : IRequest<PublishClosureCalendarResult>;

public class PublishClosureDayCommandHandler(
    ITenantDbContext db,
    ClosureAttendanceService attendance,
    ClosureNotificationService notifications)
    : IRequestHandler<PublishClosureDayCommand, PublishClosureCalendarResult>
{
    public async Task<PublishClosureCalendarResult> Handle(PublishClosureDayCommand request, CancellationToken cancellationToken)
    {
        var closure = await db.KdvClosureDays.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (closure is null)
            return PublishClosureCalendarResult.Fail(ClosureCalendarFailure.NotFound);
        if (closure.Status != ClosureStatus.Draft)
            return PublishClosureCalendarResult.Fail(ClosureCalendarFailure.NotPublishable);

        var checkedInCount = await attendance.CountCheckedInAsync(closure, cancellationToken);
        if (checkedInCount > 0 && !request.ConfirmExistingAttendance)
            return PublishClosureCalendarResult.Fail(ClosureCalendarFailure.AttendanceConfirmationRequired, checkedInCount);

        closure.Status = ClosureStatus.Published;
        closure.PublishedAt = DateTime.UtcNow;
        closure.PublishedBy = request.PublishedBy;
        closure.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var attendanceSummary = await attendance.ApplyClosureAsync(
            closure, request.PublishedBy, preservePriorState: checkedInCount > 0, cancellationToken);
        closure.AttendanceGeneratedAt = DateTime.UtcNow;
        closure.AttendanceGeneratedBy = request.PublishedBy;
        await db.SaveChangesAsync(cancellationToken);

        var notificationSummary = closure.NotifyParents
            ? await notifications.NotifyAsync(closure, ClosureNotificationKind.Published, cancellationToken)
            : new ClosureNotificationSummary(0, 0, 0, 0);

        if (closure.NotifyParents)
        {
            closure.NotificationSentAt = DateTime.UtcNow;
            closure.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var response = new PublishClosureDayResponse(
            ClosureCalendarMapper.ToResponse(closure),
            attendanceSummary.Created,
            attendanceSummary.Updated,
            false,
            new ClosureNotificationSummaryResponse(
                notificationSummary.Recipients,
                notificationSummary.PushSent,
                notificationSummary.PushFailed,
                notificationSummary.MessagesCreated));

        return PublishClosureCalendarResult.Success(response);
    }
}
