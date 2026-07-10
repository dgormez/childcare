using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

public record UpdateStaffScheduleCommand(
    Guid Id,
    Guid LocationId,
    Guid? GroupId,
    TimeOnly StartTime,
    TimeOnly EndTime) : IRequest<StaffScheduleResult>;

public class UpdateStaffScheduleCommandValidator : AbstractValidator<UpdateStaffScheduleCommand>
{
    public UpdateStaffScheduleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime);
    }
}

// FR-002/FR-003/FR-004: edit a future-dated entry only; overlap re-checked against the new
// time/location, excluding the row being edited.
public class UpdateStaffScheduleCommandHandler(ITenantDbContext db, IAdvisoryLockService advisoryLock)
    : IRequestHandler<UpdateStaffScheduleCommand, StaffScheduleResult>
{
    public async Task<StaffScheduleResult> Handle(UpdateStaffScheduleCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffSchedules.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (entry is null)
            return StaffScheduleResult.Fail(StaffScheduleFailure.NotFound);

        if (entry.Date < BelgianCalendarDay.Today())
            return StaffScheduleResult.Fail(StaffScheduleFailure.PastDate);

        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return StaffScheduleResult.Fail(StaffScheduleFailure.LocationNotFound);

        // Same eligibility rule as create (convergence finding F1) — checked unconditionally
        // since the request always carries a LocationId, whether or not it actually changed.
        var eligible = await db.StaffLocationEligibility.AnyAsync(
            e => e.StaffProfileId == entry.StaffProfileId && e.LocationId == request.LocationId, cancellationToken);
        if (!eligible)
            return StaffScheduleResult.Fail(StaffScheduleFailure.NotEligible);

        if (request.GroupId is not null)
        {
            var groupExists = await db.Groups.AnyAsync(g => g.Id == request.GroupId, cancellationToken);
            if (!groupExists)
                return StaffScheduleResult.Fail(StaffScheduleFailure.GroupNotFound);
        }

        return await advisoryLock.RunExclusiveAsync(entry.StaffProfileId, () => UpdateAsync(entry.Id, request, cancellationToken), cancellationToken);
    }

    private async Task<StaffScheduleResult> UpdateAsync(Guid entryId, UpdateStaffScheduleCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffSchedules.FirstAsync(s => s.Id == entryId, cancellationToken);

        var hasOverlap = await OverlapCheck.ExistsAsync(db, entry.StaffProfileId, entry.Date, request.StartTime, request.EndTime, excludeId: entry.Id, cancellationToken);
        if (hasOverlap)
            return StaffScheduleResult.Fail(StaffScheduleFailure.Overlap);

        entry.LocationId = request.LocationId;
        entry.GroupId = request.GroupId;
        entry.StartTime = request.StartTime;
        entry.EndTime = request.EndTime;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return StaffScheduleResult.Success(StaffScheduleMapper.ToResponse(entry));
    }
}
