using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

public record DeleteAttendanceRecordCommand(Guid Id, bool IsDirector, Guid? RequestingDeviceLocationId) : IRequest<AttendanceResult>;

public class DeleteAttendanceRecordCommandValidator : AbstractValidator<DeleteAttendanceRecordCommand>
{
    public DeleteAttendanceRecordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// No soft-delete flag on this entity (unlike ChildEvent) — a mistaken record is corrected via
// CorrectAttendanceRecordCommand, or removed outright, since attendance has no parent-facing
// view that needs a "was this deleted" audit trail (contracts/attendance-api.md).
public class DeleteAttendanceRecordCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteAttendanceRecordCommand, AttendanceResult>
{
    public async Task<AttendanceResult> Handle(DeleteAttendanceRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (record is null)
            return AttendanceResult.Fail(AttendanceFailure.NotFound);

        if (!AttendanceEditWindowPolicy.CanModify(record, request.IsDirector, request.RequestingDeviceLocationId))
            return AttendanceResult.Fail(AttendanceFailure.EditWindowExpired);

        db.AttendanceRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);

        return AttendanceResult.Success(AttendanceMapper.ToResponse(record), created: false);
    }
}
