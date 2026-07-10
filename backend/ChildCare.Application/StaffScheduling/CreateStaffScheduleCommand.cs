using System.Data.Common;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffScheduling;

public record CreateStaffScheduleCommand(
    Guid StaffProfileId,
    Guid LocationId,
    Guid? GroupId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime) : IRequest<StaffScheduleResult>;

public class CreateStaffScheduleCommandValidator : AbstractValidator<CreateStaffScheduleCommand>
{
    public CreateStaffScheduleCommandValidator()
    {
        RuleFor(x => x.StaffProfileId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime);
    }
}

// FR-001/FR-003/FR-004: create a planned shift, rejecting overlaps (same or cross-location,
// research.md-broadened FR-003) and past dates.
public class CreateStaffScheduleCommandHandler(ITenantDbContext db, IAdvisoryLockService advisoryLock)
    : IRequestHandler<CreateStaffScheduleCommand, StaffScheduleResult>
{
    public async Task<StaffScheduleResult> Handle(CreateStaffScheduleCommand request, CancellationToken cancellationToken)
    {
        if (request.Date < BelgianCalendarDay.Today())
            return StaffScheduleResult.Fail(StaffScheduleFailure.PastDate);

        var staffExists = await db.StaffProfiles.AnyAsync(s => s.Id == request.StaffProfileId, cancellationToken);
        if (!staffExists)
            return StaffScheduleResult.Fail(StaffScheduleFailure.StaffNotFound);

        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return StaffScheduleResult.Fail(StaffScheduleFailure.LocationNotFound);

        // Cross-feature consistency (convergence finding F1): VerifyPinCommand/CheckInCommand
        // already refuse a caregiver's check-in at a location they aren't eligible for
        // (RoomShiftFailure.NotEligible) — a director must not be able to plan a shift that
        // would fail at real check-in time.
        var eligible = await db.StaffLocationEligibility.AnyAsync(
            e => e.StaffProfileId == request.StaffProfileId && e.LocationId == request.LocationId, cancellationToken);
        if (!eligible)
            return StaffScheduleResult.Fail(StaffScheduleFailure.NotEligible);

        if (request.GroupId is not null)
        {
            var groupExists = await db.Groups.AnyAsync(g => g.Id == request.GroupId, cancellationToken);
            if (!groupExists)
                return StaffScheduleResult.Fail(StaffScheduleFailure.GroupNotFound);
        }

        // FR-003: serialized per staff member so two concurrent creates for the same staff
        // member cannot both pass the overlap check (research.md R2, mirrors feature 007's
        // ActivateContractCommand/IAdvisoryLockService pattern).
        return await advisoryLock.RunExclusiveAsync(request.StaffProfileId, () => CreateAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<StaffScheduleResult> CreateAsync(CreateStaffScheduleCommand request, CancellationToken cancellationToken)
    {
        var hasOverlap = await OverlapCheck.ExistsAsync(db, request.StaffProfileId, request.Date, request.StartTime, request.EndTime, excludeId: null, cancellationToken);
        if (hasOverlap)
            return StaffScheduleResult.Fail(StaffScheduleFailure.Overlap);

        var entry = new StaffSchedule
        {
            StaffProfileId = request.StaffProfileId,
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
        };

        db.StaffSchedules.Add(entry);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return StaffScheduleResult.Fail(StaffScheduleFailure.Duplicate);
        }

        return StaffScheduleResult.Success(StaffScheduleMapper.ToResponse(entry));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) => ex.InnerException is DbException { SqlState: "23505" };
}

// Shared overlap predicate (FR-003) — used by create, update, and copy-week, so the "same or
// different location" range-overlap rule is defined exactly once.
internal static class OverlapCheck
{
    public static Task<bool> ExistsAsync(
        ITenantDbContext db, Guid staffProfileId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? excludeId, CancellationToken cancellationToken) =>
        db.StaffSchedules.AnyAsync(
            s => s.StaffProfileId == staffProfileId && s.Date == date
                 && s.StartTime < endTime && startTime < s.EndTime
                 && (excludeId == null || s.Id != excludeId),
            cancellationToken);
}
