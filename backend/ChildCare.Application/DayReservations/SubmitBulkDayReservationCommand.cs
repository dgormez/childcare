using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

// Feature 030 (spec.md FR-001/FR-002/FR-003, research.md R1) — dispatches the existing
// SubmitDayReservationCommand once per child via IMediator rather than duplicating its
// validation (contract link, 013f policy, notice-hours, closure-day, exchange-day-of-week): a
// rule that blocks one child's reservation MUST NOT block the others (FR-002), which per-child
// mediator dispatch guarantees for free.
public record SubmitBulkDayReservationCommand(
    Guid TenantUserId,
    IReadOnlyList<Guid> ChildIds,
    string Type,
    DateOnly RequestedDate,
    DateOnly? ExchangeForDate,
    string? Reason) : IRequest<BulkDayReservationResponse>;

public class SubmitBulkDayReservationCommandValidator : AbstractValidator<SubmitBulkDayReservationCommand>
{
    public SubmitBulkDayReservationCommandValidator()
    {
        RuleFor(x => x.ChildIds).NotEmpty();
        RuleFor(x => x.Type).Must(t => DayReservationMapper.TryParseType(t, out _)).WithMessage("errors.day_reservations.invalid_type");
        RuleFor(x => x.RequestedDate).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public class SubmitBulkDayReservationCommandHandler(ITenantDbContext db, IMediator mediator)
    : IRequestHandler<SubmitBulkDayReservationCommand, BulkDayReservationResponse>
{
    private static readonly Dictionary<DayReservationFailure, string> ErrorKeys = new()
    {
        [DayReservationFailure.ChildNotLinked] = "errors.day_reservations.child_not_linked",
        [DayReservationFailure.NotContractedDay] = "errors.day_reservations.not_contracted_day",
        [DayReservationFailure.ClosureDay] = "errors.day_reservations.closure_day",
        [DayReservationFailure.NoContractedLocation] = "errors.day_reservations.no_contracted_location",
        [DayReservationFailure.RequestTypeDisabled] = "errors.day_reservations.request_type_disabled",
        [DayReservationFailure.NoticePeriodRequired] = "errors.day_reservations.notice_period_required",
    };

    public async Task<BulkDayReservationResponse> Handle(SubmitBulkDayReservationCommand request, CancellationToken cancellationToken)
    {
        var results = new List<BulkDayReservationResultItem>(request.ChildIds.Count);

        foreach (var childId in request.ChildIds)
        {
            var result = await mediator.Send(
                new SubmitDayReservationCommand(request.TenantUserId, childId, request.Type, request.RequestedDate, request.ExchangeForDate, request.Reason),
                cancellationToken);

            if (result.Succeeded)
            {
                results.Add(new BulkDayReservationResultItem(childId, result.Response!.ChildDisplayName, true, result.Response, null));
                continue;
            }

            // Child name is only resolved for a failure past the link check — a forged childId
            // the caller isn't linked to (ChildNotLinked) never gets its name looked up, so a
            // malformed bulk request can't be used to probe another family's child names.
            var childName = result.Failure == DayReservationFailure.ChildNotLinked
                ? string.Empty
                : (await db.Children.Where(c => c.Id == childId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefaultAsync(cancellationToken)) ?? string.Empty;

            var errorKey = ErrorKeys.GetValueOrDefault(result.Failure!.Value, "errors.day_reservations.submission_failed");
            results.Add(new BulkDayReservationResultItem(childId, childName, false, null, errorKey));
        }

        return new BulkDayReservationResponse(results);
    }
}
