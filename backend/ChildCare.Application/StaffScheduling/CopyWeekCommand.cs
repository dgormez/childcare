using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

public record CopyWeekCommand(Guid LocationId, DateOnly SourceWeekStart, DateOnly TargetWeekStart) : IRequest<CopyWeekResult>;

public class CopyWeekCommandValidator : AbstractValidator<CopyWeekCommand>
{
    public CopyWeekCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        // spec.md Assumptions: a "week" is Monday-Sunday, identified by its Monday date.
        RuleFor(x => x.SourceWeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday);
        RuleFor(x => x.TargetWeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday);
    }
}

// FR-008/FR-009/FR-009a/FR-016: bulk-copy one location's week onto another week. Skips (never
// overwrites) slots that fall on a published closure day or already have an entry — both are
// reported, never silently dropped (research.md R4).
public class CopyWeekCommandHandler(ITenantDbContext db) : IRequestHandler<CopyWeekCommand, CopyWeekResult>
{
    public async Task<CopyWeekResult> Handle(CopyWeekCommand request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return CopyWeekResult.Fail(StaffScheduleFailure.LocationNotFound);

        // FR-016: forward-planning only — target must be strictly after source and not
        // already (fully or partially) passed.
        if (request.TargetWeekStart <= request.SourceWeekStart || request.TargetWeekStart < BelgianCalendarDay.Today())
            return CopyWeekResult.Fail(StaffScheduleFailure.InvalidCopyTarget);

        var sourceWeekEnd = request.SourceWeekStart.AddDays(6);
        var sourceEntries = await db.StaffSchedules
            .Where(s => s.LocationId == request.LocationId && s.Date >= request.SourceWeekStart && s.Date <= sourceWeekEnd)
            .ToListAsync(cancellationToken);

        var dayOffset = request.TargetWeekStart.DayNumber - request.SourceWeekStart.DayNumber;
        var targetWeekEnd = request.TargetWeekStart.AddDays(6);

        // FR-009: only a Published closure day actually blocks the KDV — a Draft closure isn't
        // confirmed yet (same convention as ListBillableClosureDatesQuery).
        var closureDates = (await db.KdvClosureDays
            .Where(c => c.LocationId == request.LocationId && c.Date >= request.TargetWeekStart && c.Date <= targetWeekEnd
                        && c.Status == ClosureStatus.Published)
            .Select(c => c.Date)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var existingTargetEntries = await db.StaffSchedules
            .Where(s => s.Date >= request.TargetWeekStart && s.Date <= targetWeekEnd)
            .ToListAsync(cancellationToken);

        var skipped = new List<CopyWeekSkippedEntryResponse>();
        var toInsert = new List<StaffSchedule>();

        foreach (var source in sourceEntries.OrderBy(s => s.Date).ThenBy(s => s.StartTime))
        {
            var targetDate = source.Date.AddDays(dayOffset);

            if (closureDates.Contains(targetDate))
            {
                skipped.Add(new CopyWeekSkippedEntryResponse(targetDate, source.StaffProfileId, "closure_day"));
                continue;
            }

            // FR-009a: skip (don't overwrite) a slot that already has an entry, whether from
            // before this copy ran or staged earlier in this same copy loop.
            var conflicts = existingTargetEntries
                .Concat(toInsert)
                .Any(e => e.StaffProfileId == source.StaffProfileId && e.Date == targetDate
                          && e.StartTime < source.EndTime && source.StartTime < e.EndTime);

            if (conflicts)
            {
                skipped.Add(new CopyWeekSkippedEntryResponse(targetDate, source.StaffProfileId, "existing_entry"));
                continue;
            }

            toInsert.Add(new StaffSchedule
            {
                StaffProfileId = source.StaffProfileId,
                LocationId = source.LocationId,
                GroupId = source.GroupId,
                Date = targetDate,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
            });
        }

        if (toInsert.Count > 0)
        {
            db.StaffSchedules.AddRange(toInsert);
            await db.SaveChangesAsync(cancellationToken);
        }

        return CopyWeekResult.Success(new CopyWeekResponse(toInsert.Count, skipped));
    }
}
