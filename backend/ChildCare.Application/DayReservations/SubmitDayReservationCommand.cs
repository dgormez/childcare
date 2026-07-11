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
    IClosureCalendarReader closureCalendar) : IRequestHandler<SubmitDayReservationCommand, DayReservationResult>
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

        var reservation = new DayReservation
        {
            ChildId = request.ChildId,
            Type = type,
            RequestedDate = request.RequestedDate,
            ExchangeForDate = type == DayReservationType.Exchange ? request.ExchangeForDate : null,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            RequestedBy = request.TenantUserId,
        };

        db.DayReservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == request.ChildId, cancellationToken);
        return DayReservationResult.Success(DayReservationMapper.ToResponse(reservation, $"{child.FirstName} {child.LastName}"));
    }
}
