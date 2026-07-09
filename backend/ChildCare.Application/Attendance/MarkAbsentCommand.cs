using System.Data.Common;
using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

// Callable by either a caregiver (device token) or a director — both may set justified or
// unjustified (spec.md: descriptive data entry, not an approval gate). GroupId is needed for
// IShiftAttributionService when the caller is a device; null when a director calls this (no
// roster lookup — RecordedBy is the director's own TenantUserId instead, set by the caller).
public record MarkAbsentCommand(
    Guid ChildId, Guid LocationId, Guid? GroupId, DateOnly Date, bool AbsenceJustified, string? AbsenceReason,
    Guid? DirectorTenantUserId) : IRequest<AttendanceResult>;

public class MarkAbsentCommandValidator : AbstractValidator<MarkAbsentCommand>
{
    public MarkAbsentCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
    }
}

public class MarkAbsentCommandHandler(ITenantDbContext db, IShiftAttributionService attribution)
    : IRequestHandler<MarkAbsentCommand, AttendanceResult>
{
    public async Task<AttendanceResult> Handle(MarkAbsentCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return AttendanceResult.Fail(AttendanceFailure.ChildNotFound);

        var existing = await db.AttendanceRecords.FirstOrDefaultAsync(
            r => r.ChildId == request.ChildId && r.LocationId == request.LocationId && r.Date == request.Date,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Status == AttendanceStatus.Closure
                ? AttendanceResult.Fail(AttendanceFailure.ClosureDay)
                : AttendanceResult.Fail(AttendanceFailure.AlreadyRecorded);
        }

        var recordedBy = request.DirectorTenantUserId.HasValue
            ? [request.DirectorTenantUserId.Value]
            : (await attribution.ResolveRecordedByAsync(request.LocationId, request.GroupId ?? Guid.Empty, DateTime.UtcNow, cancellationToken)).ToList();

        var record = new AttendanceRecord
        {
            ChildId = request.ChildId,
            LocationId = request.LocationId,
            Date = request.Date,
            Status = AttendanceStatus.Absent,
            AbsenceJustified = request.AbsenceJustified,
            AbsenceReason = request.AbsenceReason,
            RecordedBy = recordedBy,
        };

        db.AttendanceRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // FR-005: raced against a concurrent check-in/absence-mark for the same key —
            // whichever write committed first wins, this one loses with the same 409.
            var winner = await db.AttendanceRecords.AsNoTracking().FirstAsync(
                r => r.ChildId == request.ChildId && r.LocationId == request.LocationId && r.Date == request.Date,
                cancellationToken);
            return AttendanceResult.Fail(winner.Status == AttendanceStatus.Closure ? AttendanceFailure.ClosureDay : AttendanceFailure.AlreadyRecorded);
        }

        return AttendanceResult.Success(AttendanceMapper.ToResponse(record), created: true);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) => ex.InnerException is DbException { SqlState: "23505" };
}
