using System.Text.Json;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record ClosureAttendanceSummary(int Created, int Updated, int Released, int Preserved);

internal record PriorAttendanceState(
    string Status,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    int? PlannedDurationMinutes,
    bool? AbsenceJustified,
    string? AbsenceReason,
    IReadOnlyList<Guid> RecordedBy);

public class ClosureAttendanceService(ITenantDbContext db, ClosureParentRecipientResolver recipients)
{
    public async Task<int> CountCheckedInAsync(KdvClosureDay closure, CancellationToken cancellationToken = default) =>
        await db.AttendanceRecords.CountAsync(
            r => r.LocationId == closure.LocationId
              && r.Date == closure.Date
              && r.Status == AttendanceStatus.Present
              && r.CheckInAt != null
              && r.CheckOutAt == null,
            cancellationToken);

    public async Task<ClosureAttendanceSummary> ApplyClosureAsync(
        KdvClosureDay closure,
        Guid confirmedBy,
        CancellationToken cancellationToken = default)
    {
        var contractedChildIds = await recipients.ResolveChildIdsAsync(closure.LocationId, closure.Date, cancellationToken);
        var existingRecords = await db.AttendanceRecords
            .Where(r => r.LocationId == closure.LocationId && r.Date == closure.Date)
            .ToListAsync(cancellationToken);
        var childIds = contractedChildIds
            .Concat(existingRecords.Select(r => r.ChildId))
            .Distinct()
            .ToList();
        var created = 0;
        var updated = 0;

        foreach (var childId in childIds)
        {
            var existing = existingRecords.FirstOrDefault(r => r.ChildId == childId);

            if (existing is null)
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    ChildId = childId,
                    LocationId = closure.LocationId,
                    Date = closure.Date,
                    Status = AttendanceStatus.Closure,
                    ClosureDayId = closure.Id,
                });
                created++;
                continue;
            }

            if (existing.Status == AttendanceStatus.Closure && existing.ClosureDayId == closure.Id)
                continue;

            if (existing.PriorStateJson is null)
            {
                existing.PriorStateJson = JsonSerializer.Serialize(new PriorAttendanceState(
                    existing.Status.ToString().ToLowerInvariant(),
                    existing.CheckInAt,
                    existing.CheckOutAt,
                    existing.PlannedDurationMinutes,
                    existing.AbsenceJustified,
                    existing.AbsenceReason,
                    existing.RecordedBy));
                existing.ClosureConfirmedBy = confirmedBy;
            }

            existing.Status = AttendanceStatus.Closure;
            existing.CheckInAt = null;
            existing.CheckOutAt = null;
            existing.AbsenceJustified = null;
            existing.AbsenceReason = null;
            existing.ClosureDayId = closure.Id;
            existing.UpdatedAt = DateTime.UtcNow;
            updated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ClosureAttendanceSummary(created, updated, 0, 0);
    }

    public async Task<ClosureAttendanceSummary> ReleaseClosureAsync(KdvClosureDay closure, CancellationToken cancellationToken = default)
    {
        var records = await db.AttendanceRecords
            .Where(r => r.ClosureDayId == closure.Id)
            .ToListAsync(cancellationToken);
        var released = 0;
        var preserved = 0;

        foreach (var record in records)
        {
            if (record.Status != AttendanceStatus.Closure)
            {
                preserved++;
                continue;
            }

            if (record.PriorStateJson is not null)
            {
                var prior = JsonSerializer.Deserialize<PriorAttendanceState>(record.PriorStateJson);
                if (prior is not null)
                {
                    record.Status = prior.Status == "absent" ? AttendanceStatus.Absent : AttendanceStatus.Present;
                    record.CheckInAt = prior.CheckInAt;
                    record.CheckOutAt = prior.CheckOutAt;
                    record.PlannedDurationMinutes = prior.PlannedDurationMinutes;
                    record.AbsenceJustified = prior.AbsenceJustified;
                    record.AbsenceReason = prior.AbsenceReason;
                    record.RecordedBy = prior.RecordedBy.ToList();
                    record.PriorStateJson = null;
                    record.ClosureConfirmedBy = null;
                    record.ClosureDayId = null;
                    record.UpdatedAt = DateTime.UtcNow;
                    released++;
                    continue;
                }
            }

            db.AttendanceRecords.Remove(record);
            released++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ClosureAttendanceSummary(0, 0, released, preserved);
    }
}
