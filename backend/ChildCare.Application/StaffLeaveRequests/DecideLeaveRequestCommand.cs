using ChildCare.Application.Common;
using ChildCare.Application.StaffScheduling;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffLeaveRequests;

// FR-010/FR-011/FR-011a, data-model.md's "On Approved" rule: director approves/rejects a
// pending leave request. Approval marks every matching StaffSchedule row Absent EXCEPT rows
// already Covered (a covered absence already has an arranged replacement — overwriting it would
// silently discard CoverStaffId, FR-011a); rows with no existing entry are left untouched.
public record DecideLeaveRequestCommand(Guid Id, bool Approve, Guid ActingTenantUserId) : IRequest<StaffLeaveRequestResult>;

public class DecideLeaveRequestCommandHandler(ITenantDbContext db, StaffLeaveRequestNotificationService notifications)
    : IRequestHandler<DecideLeaveRequestCommand, StaffLeaveRequestResult>
{
    public async Task<StaffLeaveRequestResult> Handle(DecideLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffLeaveRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (entry is null)
            return StaffLeaveRequestResult.Fail(StaffLeaveRequestFailure.NotFound);

        if (entry.Status != StaffLeaveRequestStatus.Pending)
            return StaffLeaveRequestResult.Fail(StaffLeaveRequestFailure.AlreadyDecided);

        entry.Status = request.Approve ? StaffLeaveRequestStatus.Approved : StaffLeaveRequestStatus.Rejected;
        entry.DecidedBy = request.ActingTenantUserId;
        entry.DecidedAt = DateTime.UtcNow;

        if (request.Approve)
        {
            var affected = await db.StaffSchedules
                .Where(s => s.StaffProfileId == entry.StaffProfileId
                            && s.Date >= entry.DateFrom && s.Date <= entry.DateTo
                            && s.Status != StaffScheduleStatus.Covered)
                .ToListAsync(cancellationToken);

            var absenceReason = StaffScheduleMapper.ToAbsenceReason(entry.Type);
            foreach (var scheduleEntry in affected)
            {
                scheduleEntry.Status = StaffScheduleStatus.Absent;
                scheduleEntry.AbsenceReason = absenceReason;
                scheduleEntry.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyDecisionAsync(entry, cancellationToken);

        return StaffLeaveRequestResult.Success(StaffLeaveRequestMapper.ToResponse(entry));
    }
}
