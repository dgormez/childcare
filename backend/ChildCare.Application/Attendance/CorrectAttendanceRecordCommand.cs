using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

// IsDirector/RequestingDeviceLocationId come from the caller's auth claims (endpoint layer
// resolves them), never client-supplied in the request body — mirrors UpdateChildEventCommand.
public record CorrectAttendanceRecordCommand(
    Guid Id, bool IsDirector, Guid? RequestingDeviceLocationId,
    string? Status, DateTime? CheckInAt, DateTime? CheckOutAt, bool? AbsenceJustified, string? AbsenceReason)
    : IRequest<AttendanceResult>;

public class CorrectAttendanceRecordCommandValidator : AbstractValidator<CorrectAttendanceRecordCommand>
{
    public CorrectAttendanceRecordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class CorrectAttendanceRecordCommandHandler(ITenantDbContext db) : IRequestHandler<CorrectAttendanceRecordCommand, AttendanceResult>
{
    public async Task<AttendanceResult> Handle(CorrectAttendanceRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (record is null)
            return AttendanceResult.Fail(AttendanceFailure.NotFound);

        if (!AttendanceEditWindowPolicy.CanModify(record, request.IsDirector, request.RequestingDeviceLocationId))
            return AttendanceResult.Fail(AttendanceFailure.EditWindowExpired);

        AttendanceStatus? requestedStatus = null;
        if (request.Status is not null)
        {
            if (!Enum.TryParse<AttendanceStatus>(request.Status, ignoreCase: true, out var parsed))
                throw new ValidationException([new ValidationFailure("status", "errors.validation")]);

            // FR-015: closure is only ever set by a future feature 011 mechanism, never a
            // direct correction write.
            if (parsed == AttendanceStatus.Closure)
                return AttendanceResult.Fail(AttendanceFailure.ClosureStatusImmutable);

            requestedStatus = parsed;
        }

        var mergedStatus = requestedStatus ?? record.Status;
        var mergedCheckInAt = request.CheckInAt ?? record.CheckInAt;
        var mergedAbsenceJustified = request.AbsenceJustified ?? record.AbsenceJustified;

        // FR-011a: the same status/field invariants creation enforces apply to corrections too —
        // present requires checkInAt; absent requires absenceJustified and must not retain a
        // stale checkInAt/checkOutAt/absenceJustified value from a prior state.
        if (mergedStatus == AttendanceStatus.Present && mergedCheckInAt is null)
            throw new ValidationException([new ValidationFailure("checkInAt", "errors.attendance.check_in_at_required")]);

        if (mergedStatus == AttendanceStatus.Absent && mergedAbsenceJustified is null)
            throw new ValidationException([new ValidationFailure("absenceJustified", "errors.attendance.absence_justified_required")]);

        record.Status = mergedStatus;

        if (mergedStatus == AttendanceStatus.Present)
        {
            record.CheckInAt = mergedCheckInAt;
            record.CheckOutAt = request.CheckOutAt ?? record.CheckOutAt;
            record.AbsenceJustified = null;
            record.AbsenceReason = null;
        }
        else if (mergedStatus == AttendanceStatus.Absent)
        {
            record.CheckInAt = null;
            record.CheckOutAt = null;
            record.AbsenceJustified = mergedAbsenceJustified;
            record.AbsenceReason = request.AbsenceReason ?? record.AbsenceReason;
        }

        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return AttendanceResult.Success(AttendanceMapper.ToResponse(record), created: false);
    }
}
