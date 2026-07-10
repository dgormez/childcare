using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

public record DeleteStaffScheduleCommand(Guid Id) : IRequest<DeleteStaffScheduleResult>;

// FR-002/FR-004: delete a future-dated entry only.
public class DeleteStaffScheduleCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteStaffScheduleCommand, DeleteStaffScheduleResult>
{
    public async Task<DeleteStaffScheduleResult> Handle(DeleteStaffScheduleCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffSchedules.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (entry is null)
            return DeleteStaffScheduleResult.Fail(StaffScheduleFailure.NotFound);

        if (entry.Date < BelgianCalendarDay.Today())
            return DeleteStaffScheduleResult.Fail(StaffScheduleFailure.PastDate);

        db.StaffSchedules.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return DeleteStaffScheduleResult.Success();
    }
}
