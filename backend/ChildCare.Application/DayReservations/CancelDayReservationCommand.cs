using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

public record CancelDayReservationCommand(Guid TenantUserId, Guid Id) : IRequest<DayReservationResult>;

public class CancelDayReservationCommandValidator : AbstractValidator<CancelDayReservationCommand>
{
    public CancelDayReservationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class CancelDayReservationCommandHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IAdvisoryLockService advisoryLock) : IRequestHandler<CancelDayReservationCommand, DayReservationResult>
{
    public async Task<DayReservationResult> Handle(CancelDayReservationCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return DayReservationResult.Fail(DayReservationFailure.ChildNotLinked);

        var result = await advisoryLock.RunExclusiveAsync(
            request.Id,
            () => DecideAsync(request, cancellationToken),
            cancellationToken);

        return result.Succeeded
            ? DayReservationResult.Success(DayReservationMapper.ToResponse(result.Reservation!, result.ChildDisplayName!))
            : DayReservationResult.Fail(result.Failure!.Value);
    }

    private async Task<(bool Succeeded, DayReservationFailure? Failure, Domain.Entities.DayReservation? Reservation, string? ChildDisplayName)> DecideAsync(
        CancelDayReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await db.DayReservations.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (reservation is null)
            return (false, DayReservationFailure.NotFound, null, null);

        // FR-014: only the requesting parent's own request — not merely any parent linked to the
        // same child (a child can have two contacts, e.g. both parents, each with their own
        // account; one shouldn't cancel the other's submitted request).
        if (reservation.RequestedBy != request.TenantUserId)
            return (false, DayReservationFailure.ChildNotLinked, null, null);

        if (reservation.Status != DayReservationStatus.Pending)
            return (false, DayReservationFailure.NotPending, null, null);

        reservation.Status = DayReservationStatus.Cancelled;
        reservation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == reservation.ChildId, cancellationToken);
        return (true, null, reservation, $"{child.FirstName} {child.LastName}");
    }
}
