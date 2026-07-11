using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

public record RejectDayReservationCommand(Guid DirectorTenantUserId, Guid Id, string? DirectorNotes) : IRequest<DayReservationResult>;

public class RejectDayReservationCommandHandler(
    ITenantDbContext db,
    IAdvisoryLockService advisoryLock,
    DayReservationNotificationService notifications) : IRequestHandler<RejectDayReservationCommand, DayReservationResult>
{
    public async Task<DayReservationResult> Handle(RejectDayReservationCommand request, CancellationToken cancellationToken)
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
        RejectDayReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await db.DayReservations.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (reservation is null)
            return (false, DayReservationFailure.NotFound, null, null);
        if (reservation.Status != DayReservationStatus.Pending)
            return (false, DayReservationFailure.NotPending, null, null);

        reservation.Status = DayReservationStatus.Rejected;
        reservation.DirectorNotes = string.IsNullOrWhiteSpace(request.DirectorNotes) ? null : request.DirectorNotes.Trim();
        reservation.DecidedBy = request.DirectorTenantUserId;
        reservation.DecidedAt = DateTime.UtcNow;
        reservation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == reservation.ChildId, cancellationToken);
        return (true, null, reservation, $"{child.FirstName} {child.LastName}");
    }
}
