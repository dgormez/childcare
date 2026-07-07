using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using ChildCare.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

// FR-007: the request carries the complete set of new terms — a full replacement, reusing
// CreateContractCommandValidator's field rules (including FR-010's consent-defaults-to-false
// behavior) — plus the effective start date the new terms begin on.
public record AmendContractCommand(
    Guid Id,
    DateOnly EffectiveStartDate,
    Guid LocationId,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayRequest> ContractedDays,
    int DailyRateCents,
    ContractConsentRequest? Consent) : IRequest<ContractResult>;

public class AmendContractCommandValidator : AbstractValidator<AmendContractCommand>
{
    private static readonly DayOfWeek[] ValidWeekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public AmendContractCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate >= x.EffectiveStartDate)
            .WithMessage("errors.contract.end_date_before_start_date")
            .OverridePropertyName("EndDate");

        RuleFor(x => x.ContractedDays)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.contract.weekday_required")
            .Must(days => days.All(d => ValidWeekdays.Contains(d.Weekday)))
                .WithMessage("errors.contract.weekday_invalid")
            .Must(days => days.Select(d => d.Weekday).Distinct().Count() == days.Count)
                .WithMessage("errors.contract.weekday_invalid")
            .Must(days => days.All(d => d.StartTime < d.EndTime))
                .WithMessage("errors.contract.time_range_invalid");

        RuleFor(x => x.DailyRateCents)
            .GreaterThan(0).WithMessage("errors.contract.daily_rate_invalid");
    }
}

public class AmendContractCommandHandler(ITenantDbContext db, IAdvisoryLockService advisoryLock)
    : IRequestHandler<AmendContractCommand, ContractResult>
{
    public async Task<ContractResult> Handle(AmendContractCommand request, CancellationToken cancellationToken)
    {
        var current = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (current is null)
            return ContractResult.Fail(ContractFailure.NotFound);

        if (current.Status != ContractStatus.Active)
            return ContractResult.Fail(ContractFailure.NotActive);

        if (request.EffectiveStartDate <= current.StartDate)
            return ContractResult.Fail(ContractFailure.AmendmentStartDateInvalid);

        var failure = await advisoryLock.RunExclusiveAsync(current.ChildId, async () =>
        {
            // Staged in-memory only — not yet saved. ContractActivationChecker is passed
            // current.Id as excludeContractId below, since its query would otherwise still see
            // this row as Active at the SQL/persisted level (research.md R2, R5).
            current.Status = ContractStatus.Ended;
            current.EndDate = request.EffectiveStartDate.AddDays(-1);
            current.UpdatedAt = DateTime.UtcNow;

            var successor = new Contract
            {
                ChildId = current.ChildId,
                LocationId = request.LocationId,
                PreviousContractId = current.Id,
                StartDate = request.EffectiveStartDate,
                EndDate = request.EndDate,
                ContractedDays = request.ContractedDays
                    .Select(d => new ContractedDay { Weekday = d.Weekday, StartTime = d.StartTime, EndTime = d.EndTime })
                    .ToList(),
                DailyRateCents = request.DailyRateCents,
                Status = ContractStatus.Draft,
                Consent = CreateContractCommandHandler.ToConsent(request.Consent),
            };
            db.Contracts.Add(successor);

            var checkFailure = await ContractActivationChecker.CheckAndActivateAsync(
                db, successor, cancellationToken, excludeContractId: current.Id);
            return (Failure: checkFailure, Successor: successor);
        }, cancellationToken);

        // If the check failed, neither the in-memory Ended transition nor the new draft was
        // ever saved (ContractActivationChecker only calls SaveChangesAsync on success) — the
        // request-scoped ITenantDbContext still holds both as uncommitted changes, which is
        // discarded when the request ends, so nothing persists.
        return failure.Failure is null
            ? ContractResult.Success(ContractMapper.ToResponse(failure.Successor))
            : ContractResult.Fail(failure.Failure.Value);
    }
}
