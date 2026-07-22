using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

// FR-006/FR-007/FR-014/FR-018: director assigns a replacement for an Absent StaffSchedule row.
// {id} is the absent row's own id. Sets CoverStaffId on the original (data-model.md's corrected
// placement, research.md), creates a new immediately-visible Covered row for the replacement
// (bypassing publish/draft, research.md R4), and notifies both staff members.
public record AssignCoverCommand(Guid Id, Guid CoverStaffProfileId, Guid ActingTenantUserId) : IRequest<AssignCoverResult>;

public class AssignCoverCommandValidator : AbstractValidator<AssignCoverCommand>
{
    public AssignCoverCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CoverStaffProfileId).NotEmpty();
    }
}

public class AssignCoverCommandHandler(ITenantDbContext db, IAdvisoryLockService advisoryLock, StaffScheduleNotificationService notifications)
    : IRequestHandler<AssignCoverCommand, AssignCoverResult>
{
    public async Task<AssignCoverResult> Handle(AssignCoverCommand request, CancellationToken cancellationToken)
    {
        var original = await db.StaffSchedules.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (original is null)
            return AssignCoverResult.Fail(StaffScheduleFailure.NotFound);

        if (original.Status != StaffScheduleStatus.Absent)
            return AssignCoverResult.Fail(StaffScheduleFailure.NoAbsentAssignment);

        // A staff member cannot cover their own absence (data-model.md's constraint).
        if (request.CoverStaffProfileId == original.StaffProfileId)
            return AssignCoverResult.Fail(StaffScheduleFailure.NotEligible);

        var eligible = await db.StaffLocationEligibility.AnyAsync(
            e => e.StaffProfileId == request.CoverStaffProfileId && e.LocationId == original.LocationId, cancellationToken);
        if (!eligible)
            return AssignCoverResult.Fail(StaffScheduleFailure.NotEligible);

        // FR-018/research.md R5: serialized per replacement staff member so two concurrent
        // assignments for the same replacement cannot both pass the overlap check.
        return await advisoryLock.RunExclusiveAsync(request.CoverStaffProfileId, () => AssignAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<AssignCoverResult> AssignAsync(AssignCoverCommand request, CancellationToken cancellationToken)
    {
        var original = await db.StaffSchedules.FirstAsync(s => s.Id == request.Id, cancellationToken);

        // FR-014's write-side enforcement (not just the read-side candidates list): reject an
        // ineligible/conflicting coverStaffProfileId even if it was never offered by
        // GetSickCoverCandidatesQuery.
        var hasOverlap = await OverlapCheck.ExistsAsync(
            db, request.CoverStaffProfileId, original.Date, original.StartTime, original.EndTime, excludeId: null, cancellationToken);
        if (hasOverlap)
            return AssignCoverResult.Fail(StaffScheduleFailure.Overlap);

        var now = DateTime.UtcNow;
        var coverEntry = new StaffSchedule
        {
            StaffProfileId = request.CoverStaffProfileId,
            LocationId = original.LocationId,
            GroupId = original.GroupId,
            Date = original.Date,
            StartTime = original.StartTime,
            EndTime = original.EndTime,
            Status = StaffScheduleStatus.Covered,
            CreatedBy = request.ActingTenantUserId,
            // research.md R4: immediately visible regardless of the week's own publish state —
            // an urgent operational change, not forward planning.
            IsPublished = true,
            PublishedAt = now,
        };

        db.StaffSchedules.Add(coverEntry);

        original.CoverStaffId = request.CoverStaffProfileId;
        original.CreatedBy = request.ActingTenantUserId;
        original.UpdatedAt = now;
        if (!original.IsPublished)
        {
            original.IsPublished = true;
            original.PublishedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAbsentStaffCoveredAsync(original.StaffProfileId, original.Id, cancellationToken);
        await notifications.NotifyCoverStaffAssignedAsync(coverEntry.StaffProfileId, coverEntry.Id, cancellationToken);

        return AssignCoverResult.Success(new AssignCoverResponse(
            StaffScheduleMapper.ToResponse(original), StaffScheduleMapper.ToResponse(coverEntry)));
    }
}
