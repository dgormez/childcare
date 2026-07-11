using ChildCare.Application.Attendance;
using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

public record ApproveDayReservationCommand(Guid DirectorTenantUserId, Guid Id, bool? AbsenceJustified) : IRequest<DayReservationResult>;

/// <summary>
/// FR-008/FR-010/FR-011/FR-012/FR-016. Absence approval delegates the actual attendance write to
/// the existing MarkAbsentCommand (research.md R1) rather than duplicating its closure-day guard;
/// Extra/Exchange approvals only transition this row's own Status (research.md R2). The whole
/// check-then-act sequence runs inside IAdvisoryLockService.RunExclusiveAsync keyed on the
/// reservation's own Id (feature 007's precedent for serializing concurrent requests against the
/// same aggregate, research.md) — this closes FR-016 without a lost-update window: a concurrent
/// reject can never interleave between "attendance written" and "status flipped to approved."
/// </summary>
public class ApproveDayReservationCommandHandler(
    ITenantDbContext db,
    IMediator mediator,
    IAdvisoryLockService advisoryLock,
    DayReservationNotificationService notifications) : IRequestHandler<ApproveDayReservationCommand, DayReservationResult>
{
    public async Task<DayReservationResult> Handle(ApproveDayReservationCommand request, CancellationToken cancellationToken)
    {
        var result = await advisoryLock.RunExclusiveAsync(
            request.Id,
            () => DecideAsync(request, cancellationToken),
            cancellationToken);

        if (result.Succeeded)
            await notifications.NotifyDecisionAsync(result.Reservation!, cancellationToken);

        return result.Succeeded
            ? DayReservationResult.Success(DayReservationMapper.ToResponse(result.Reservation!, result.ChildDisplayName!))
            : DayReservationResult.Fail(result.Failure!.Value);
    }

    private async Task<(bool Succeeded, DayReservationFailure? Failure, Domain.Entities.DayReservation? Reservation, string? ChildDisplayName)> DecideAsync(
        ApproveDayReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await db.DayReservations.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (reservation is null)
            return (false, DayReservationFailure.NotFound, null, null);
        if (reservation.Status != DayReservationStatus.Pending)
            return (false, DayReservationFailure.NotPending, null, null);

        if (reservation.Type == DayReservationType.Absence)
        {
            if (request.AbsenceJustified is null)
                return (false, DayReservationFailure.MissingJustifiedFlag, null, null);

            // research.md R7: resolve which location this absence pertains to from the child's
            // active contract covering RequestedDate's weekday.
            var locationId = await db.Contracts
                .Where(c => c.ChildId == reservation.ChildId && c.Status == ContractStatus.Active)
                .SelectMany(c => c.ContractedDays, (c, d) => new { c.LocationId, d.Weekday })
                .Where(x => x.Weekday == reservation.RequestedDate.DayOfWeek)
                .Select(x => (Guid?)x.LocationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (locationId is null)
                return (false, DayReservationFailure.NoContractedLocation, null, null);

            var attendanceResult = await mediator.Send(
                new MarkAbsentCommand(
                    reservation.ChildId, locationId.Value, null, reservation.RequestedDate,
                    request.AbsenceJustified.Value, reservation.Reason, request.DirectorTenantUserId),
                cancellationToken);
            if (!attendanceResult.Succeeded)
            {
                return (false, attendanceResult.Failure == AttendanceFailure.ClosureDay
                    ? DayReservationFailure.ClosureDayConflict
                    : DayReservationFailure.NotPending, null, null);
            }

            reservation.AbsenceJustified = request.AbsenceJustified;
        }

        reservation.Status = DayReservationStatus.Approved;
        reservation.DecidedBy = request.DirectorTenantUserId;
        reservation.DecidedAt = DateTime.UtcNow;
        reservation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == reservation.ChildId, cancellationToken);
        return (true, null, reservation, $"{child.FirstName} {child.LastName}");
    }
}
