using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

// FR-005/FR-005a: the acting staff member (resolved from the JWT, FR-015a — never a
// client-supplied id) reports themselves sick for today (or tomorrow, per spec.md's opening-time
// cutoff, resolved server-side). Idempotent on a repeated call for an already-Absent day: no
// duplicate StaffLeaveRequest, no duplicate director alert.
public record ReportSickCommand(Guid TenantUserId) : IRequest<ReportSickResult>;

public class ReportSickCommandHandler(
    ITenantDbContext db,
    IAdvisoryLockService advisoryLock,
    StaffScheduleNotificationService scheduleNotifications)
    : IRequestHandler<ReportSickCommand, ReportSickResult>
{
    // spec.md Assumptions: reports made before a location's normal opening time apply to today;
    // reports made after count as tomorrow's notice. No per-location opening-time configuration
    // exists in this codebase yet, so a single fixed cutoff (07:00 Europe/Brussels) is used —
    // plan-phase configuration detail per spec.md, not a product decision.
    private static readonly TimeOnly Cutoff = new(7, 0);

    public async Task<ReportSickResult> Handle(ReportSickCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return ReportSickResult.Fail(StaffScheduleFailure.ProfileNotFound);

        return await advisoryLock.RunExclusiveAsync(profile.Id, () => ReportAsync(profile, cancellationToken), cancellationToken);
    }

    private async Task<ReportSickResult> ReportAsync(StaffProfile profile, CancellationToken cancellationToken)
    {
        var now = BelgianCalendarDay.ToLocalDate(DateTime.UtcNow);
        var nowTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels")));
        var resolvedDate = nowTime < Cutoff ? now : now.AddDays(1);

        var entry = await db.StaffSchedules.FirstOrDefaultAsync(
            s => s.StaffProfileId == profile.Id && s.Date == resolvedDate, cancellationToken);

        // FR-005a: already reported for this date — idempotent, no duplicate leave request or
        // director alert.
        if (entry is not null && entry.Status == StaffScheduleStatus.Absent)
            return ReportSickResult.Success(StaffScheduleMapper.ToResponse(entry));

        var alreadyReported = await db.StaffLeaveRequests.AnyAsync(
            r => r.StaffProfileId == profile.Id && r.Type == StaffLeaveRequestType.Sick
                 && r.DateFrom == resolvedDate && r.DateTo == resolvedDate, cancellationToken);

        if (entry is not null)
        {
            entry.Status = StaffScheduleStatus.Absent;
            entry.AbsenceReason = AbsenceReason.Sick;
            entry.UpdatedAt = DateTime.UtcNow;
            // A same-day sick report bypasses the normal publish/draft gate (research.md R4) —
            // the staff member must see their own now-absent status immediately.
            if (!entry.IsPublished)
            {
                entry.IsPublished = true;
                entry.PublishedAt = DateTime.UtcNow;
            }
        }

        if (!alreadyReported)
        {
            db.StaffLeaveRequests.Add(new StaffLeaveRequest
            {
                StaffProfileId = profile.Id,
                Type = StaffLeaveRequestType.Sick,
                DateFrom = resolvedDate,
                DateTo = resolvedDate,
                Status = StaffLeaveRequestStatus.Approved,
                DecidedBy = null,
                DecidedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!alreadyReported)
        {
            var staffName = $"{profile.FirstName} {profile.LastName}";
            await scheduleNotifications.NotifyDirectorsOfSickReportAsync(
                entry ?? new StaffSchedule { Id = Guid.Empty, StaffProfileId = profile.Id, Date = resolvedDate },
                staffName,
                cancellationToken);
        }

        return ReportSickResult.Success(entry is null ? null : StaffScheduleMapper.ToResponse(entry));
    }
}
