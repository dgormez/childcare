using ChildCare.Application.Attendance;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

public record SubmitDayReservationCommand(
    Guid TenantUserId,
    Guid ChildId,
    string Type,
    DateOnly RequestedDate,
    DateOnly? ExchangeForDate,
    string? Reason) : IRequest<DayReservationResult>;

public class SubmitDayReservationCommandValidator : AbstractValidator<SubmitDayReservationCommand>
{
    public SubmitDayReservationCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
        RuleFor(x => x.Type).Must(t => DayReservationMapper.TryParseType(t, out _)).WithMessage("errors.day_reservations.invalid_type");
        RuleFor(x => x.RequestedDate).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(2000);

        // FR-002: "more than 1 day in the past" evaluated against BelgianCalendarDay
        // (research.md/spec.md Assumptions), not just Type = Absence — a request for a past
        // date makes no sense for any of the three types.
        RuleFor(x => x.RequestedDate)
            .GreaterThanOrEqualTo(_ => BelgianCalendarDay.Today().AddDays(-1))
            .WithMessage("errors.day_reservations.past_date");

        // FR-003: exchange requires the source day being given up.
        RuleFor(x => x.ExchangeForDate)
            .NotNull()
            .WithMessage("errors.day_reservations.missing_exchange_date")
            .When(x => string.Equals(x.Type, "exchange", StringComparison.OrdinalIgnoreCase));
    }
}

public class SubmitDayReservationCommandHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IClosureCalendarReader closureCalendar,
    ReservationPolicyResolver policyResolver,
    IMediator mediator) : IRequestHandler<SubmitDayReservationCommand, DayReservationResult>
{
    public async Task<DayReservationResult> Handle(SubmitDayReservationCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return DayReservationResult.Fail(DayReservationFailure.ChildNotLinked);

        var isContactOfChild = await db.ChildContacts
            .AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId, cancellationToken);
        if (!isContactOfChild)
            return DayReservationResult.Fail(DayReservationFailure.ChildNotLinked);

        DayReservationMapper.TryParseType(request.Type, out var type);

        if (type == DayReservationType.Exchange)
        {
            // FR-003: exchangeForDate must be an actual contracted weekday for this child
            // (research.md R6) — checked across any active contract, any location.
            var hasContractedDay = await db.Contracts
                .Where(c => c.ChildId == request.ChildId && c.Status == ContractStatus.Active)
                .SelectMany(c => c.ContractedDays)
                .AnyAsync(d => d.Weekday == request.ExchangeForDate!.Value.DayOfWeek, cancellationToken);
            if (!hasContractedDay)
                return DayReservationResult.Fail(DayReservationFailure.NotContractedDay);

            // FR-004: the exchange target date must not be a published closure day. Location is
            // ambiguous without a resolved contract match, so this checks against every location
            // the child holds an active contract at (research.md R6/R7) — a closure on any of
            // them blocks the request, since the exchange target is inherently tied to whichever
            // location the source contracted day belongs to.
            var locationIds = await db.Contracts
                .Where(c => c.ChildId == request.ChildId && c.Status == ContractStatus.Active)
                .Select(c => c.LocationId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var locationId in locationIds)
            {
                if (await closureCalendar.IsPublishedClosureDateAsync(locationId, request.RequestedDate, cancellationToken))
                    return DayReservationResult.Fail(DayReservationFailure.ClosureDay);
            }
        }

        // Feature 013f FR-007/FR-012/FR-017: resolve the effective per-location policy before
        // ever creating the row — disabled types never reach the table at all, and the
        // notice-hours window is enforced against the same candidate-location set.
        var policy = await policyResolver.ResolveAsync(request.ChildId, type, request.RequestedDate, cancellationToken);
        if (policy.Mode == ReservationRequestMode.Disabled)
            return DayReservationResult.Fail(DayReservationFailure.RequestTypeDisabled);

        if (policy.NoticeHours > 0)
        {
            var (requestedDateStartUtc, _) = BelgianCalendarDay.UtcRangeFor(request.RequestedDate);
            if (requestedDateStartUtc - DateTime.UtcNow < TimeSpan.FromHours(policy.NoticeHours))
                return DayReservationResult.Fail(DayReservationFailure.NoticePeriodRequired);
        }

        var reservation = new DayReservation
        {
            ChildId = request.ChildId,
            Type = type,
            RequestedDate = request.RequestedDate,
            ExchangeForDate = type == DayReservationType.Exchange ? request.ExchangeForDate : null,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            RequestedBy = request.TenantUserId,
        };

        if (policy.Mode == ReservationRequestMode.Informational)
        {
            // FR-008/FR-009/Clarifications: auto-approval applies the identical downstream
            // effect a director's approval would — for absence, that's the attendance
            // pre-registration (via the same MarkAbsentCommand ApproveDayReservationCommand
            // uses); a closure-day conflict here fails submission itself rather than silently
            // auto-approving into an invalid state.
            if (type == DayReservationType.Absence)
            {
                var locationId = await db.Contracts
                    .Where(c => c.ChildId == request.ChildId && c.Status == ContractStatus.Active)
                    .SelectMany(c => c.ContractedDays, (c, d) => new { c.LocationId, d.Weekday })
                    .Where(x => x.Weekday == request.RequestedDate.DayOfWeek)
                    .Select(x => (Guid?)x.LocationId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (locationId is null)
                    return DayReservationResult.Fail(DayReservationFailure.NoContractedLocation);

                // DirectorTenantUserId is null (not the parent's id) — a parent isn't "who
                // recorded this," the same way a caregiver device-token absence-mark isn't
                // director-attributed; RecordedBy falls through to shift attribution, same as
                // that path (MarkAbsentCommandHandler).
                var attendanceResult = await mediator.Send(
                    new MarkAbsentCommand(request.ChildId, locationId.Value, null, request.RequestedDate, true, reservation.Reason, null),
                    cancellationToken);
                if (!attendanceResult.Succeeded)
                {
                    return DayReservationResult.Fail(attendanceResult.Failure == AttendanceFailure.ClosureDay
                        ? DayReservationFailure.ClosureDay
                        : DayReservationFailure.NoContractedLocation);
                }

                reservation.AbsenceJustified = true;
            }

            reservation.Status = DayReservationStatus.Approved;
            reservation.DecidedBy = null; // research.md R1 — null DecidedBy signals a system decision.
            reservation.DecidedAt = DateTime.UtcNow;
        }

        db.DayReservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == request.ChildId, cancellationToken);
        return DayReservationResult.Success(DayReservationMapper.ToResponse(reservation, $"{child.FirstName} {child.LastName}"));
    }
}
