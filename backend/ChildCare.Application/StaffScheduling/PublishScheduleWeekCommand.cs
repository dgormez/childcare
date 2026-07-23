using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

// FR-001, contracts/staff-app-api.md: publishes (or, with Unpublish, un-publishes) every
// StaffSchedule row for one (LocationId, WeekStart) week. Behaviorally week-granular, but the
// underlying IsPublished column stays per-row (research.md R4) so a later single-row change
// (sick cover, last-minute edit) can flip visible on its own without a full re-publish.
public record PublishScheduleWeekCommand(Guid LocationId, DateOnly WeekStart, bool Unpublish, Guid ActingTenantUserId)
    : IRequest<PublishScheduleWeekResult>;

public class PublishScheduleWeekCommandValidator : AbstractValidator<PublishScheduleWeekCommand>
{
    public PublishScheduleWeekCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        // spec.md Assumptions: a "week" is Monday-Sunday, identified by its Monday date —
        // matches feature 012's CopyWeekCommand convention.
        RuleFor(x => x.WeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday);
    }
}

public class PublishScheduleWeekCommandHandler(ITenantDbContext db, StaffScheduleNotificationService notifications)
    : IRequestHandler<PublishScheduleWeekCommand, PublishScheduleWeekResult>
{
    public async Task<PublishScheduleWeekResult> Handle(PublishScheduleWeekCommand request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return PublishScheduleWeekResult.Fail(StaffScheduleFailure.LocationNotFound);

        var weekEnd = request.WeekStart.AddDays(6);
        var entries = await db.StaffSchedules
            .Where(s => s.LocationId == request.LocationId && s.Date >= request.WeekStart && s.Date <= weekEnd)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var affectedStaffProfileIds = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (request.Unpublish)
            {
                entry.IsPublished = false;
                entry.PublishedAt = null;
            }
            else if (!entry.IsPublished)
            {
                entry.IsPublished = true;
                entry.PublishedAt = now;
                affectedStaffProfileIds.Add(entry.StaffProfileId);
            }

            entry.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        // FR-008/SC-004: notify every distinct affected staff member exactly once, publish only
        // (never on unpublish, contracts/staff-app-api.md).
        if (!request.Unpublish)
            await notifications.NotifySchedulePublishedAsync(affectedStaffProfileIds, cancellationToken);

        return PublishScheduleWeekResult.Success(new PublishScheduleWeekResponse(entries.Count));
    }
}
