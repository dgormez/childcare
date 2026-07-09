using System.Data.Common;
using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

// LocationId/GroupId/RecordedByDeviceId come from the recording device's own JWT claims
// (endpoint layer resolves them, mirrors RecordChildEventCommand's pattern) — never
// client-supplied.
public record CheckInCommand(Guid ChildId, Guid LocationId, Guid GroupId, DateOnly Date) : IRequest<AttendanceResult>;

public class CheckInCommandValidator : AbstractValidator<CheckInCommand>
{
    public CheckInCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
    }
}

public class CheckInCommandHandler(
    ITenantDbContext db, IShiftAttributionService attribution, PlannedDurationCalculator plannedDuration)
    : IRequestHandler<CheckInCommand, AttendanceResult>
{
    public async Task<AttendanceResult> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return AttendanceResult.Fail(AttendanceFailure.ChildNotFound);

        var existing = await db.AttendanceRecords.FirstOrDefaultAsync(
            r => r.ChildId == request.ChildId && r.LocationId == request.LocationId && r.Date == request.Date,
            cancellationToken);

        if (existing is not null)
            return await ApplyCheckInAsync(existing, request, cancellationToken);

        var recordedBy = await attribution.ResolveRecordedByAsync(request.LocationId, request.GroupId, DateTime.UtcNow, cancellationToken);
        var duration = await plannedDuration.CalculateAsync(request.ChildId, request.LocationId, request.Date, cancellationToken);

        var record = new AttendanceRecord
        {
            ChildId = request.ChildId,
            LocationId = request.LocationId,
            Date = request.Date,
            Status = AttendanceStatus.Present,
            CheckInAt = DateTime.UtcNow,
            PlannedDurationMinutes = duration,
            RecordedBy = recordedBy.ToList(),
        };

        db.AttendanceRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // The failed insert stays tracked as `Added` after SaveChangesAsync throws — EF
            // Core does not revert entity state on failure. Left alone, the next SaveChangesAsync
            // call (ApplyCheckInAsync's transition path, below) would retry inserting this same
            // row and hit the identical unique violation again, uncaught. Remove()-ing an
            // Added-state entity detaches it (nothing to delete, since it was never persisted) —
            // works through ITenantDbContext's public DbSet surface, no DbContext.Entry needed.
            db.AttendanceRecords.Remove(record);

            // FR-003/FR-012: a concurrent check-in (or absence-mark, FR-005) won the race —
            // re-read the committed row rather than blindly failing.
            var winner = await db.AttendanceRecords.AsNoTracking().FirstAsync(
                r => r.ChildId == request.ChildId && r.LocationId == request.LocationId && r.Date == request.Date,
                cancellationToken);
            return winner.Status == AttendanceStatus.Absent
                ? await ApplyCheckInAsync(winner, request, cancellationToken)
                : AttendanceResult.Fail(winner.Status == AttendanceStatus.Closure ? AttendanceFailure.ClosureDay : AttendanceFailure.AlreadyRecorded);
        }

        return AttendanceResult.Success(AttendanceMapper.ToResponse(record), created: true);
    }

    // FR-001a: a check-in against an existing absent record transitions it to present rather
    // than conflicting; FR-012: against an existing present record it's a duplicate; FR-015: a
    // closure record rejects any manual check-in.
    private async Task<AttendanceResult> ApplyCheckInAsync(AttendanceRecord existing, CheckInCommand request, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case AttendanceStatus.Present:
                return AttendanceResult.Fail(AttendanceFailure.AlreadyRecorded);
            case AttendanceStatus.Closure:
                return AttendanceResult.Fail(AttendanceFailure.ClosureDay);
        }

        // If `existing` came from an untracked re-read (post-race), reattach the real tracked
        // instance before mutating it.
        var tracked = db.AttendanceRecords.Local.FirstOrDefault(r => r.Id == existing.Id)
            ?? await db.AttendanceRecords.FirstAsync(r => r.Id == existing.Id, cancellationToken);

        var recordedBy = await attribution.ResolveRecordedByAsync(request.LocationId, request.GroupId, DateTime.UtcNow, cancellationToken);
        var duration = await plannedDuration.CalculateAsync(request.ChildId, request.LocationId, request.Date, cancellationToken);

        tracked.Status = AttendanceStatus.Present;
        tracked.CheckInAt = DateTime.UtcNow;
        tracked.CheckOutAt = null;
        tracked.AbsenceJustified = null;
        tracked.AbsenceReason = null;
        tracked.PlannedDurationMinutes = duration;
        tracked.RecordedBy = recordedBy.ToList();
        tracked.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return AttendanceResult.Success(AttendanceMapper.ToResponse(tracked), created: false);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) => ex.InnerException is DbException { SqlState: "23505" };
}
