using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using ChildCare.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

// FR-001a: full replacement of a draft's terms — ChildId/LocationId are never accepted here
// and cannot be changed by an edit.
public record UpdateContractCommand(
    Guid Id,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayRequest> ContractedDays,
    int DailyRateCents,
    ContractConsentRequest? Consent) : IRequest<ContractResult>;

public class UpdateContractCommandValidator : AbstractValidator<UpdateContractCommand>
{
    private static readonly DayOfWeek[] ValidWeekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public UpdateContractCommandValidator()
    {
        RuleFor(x => x.StartDate)
            .NotNull().WithMessage("errors.contract.start_date_required");

        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.EndDate >= x.StartDate)
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

public class UpdateContractCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateContractCommand, ContractResult>
{
    public async Task<ContractResult> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contract is null)
            return ContractResult.Fail(ContractFailure.NotFound);

        if (contract.Status != ContractStatus.Draft)
            return ContractResult.Fail(ContractFailure.NotDraft);

        contract.StartDate = request.StartDate!.Value;
        contract.EndDate = request.EndDate;
        contract.ContractedDays = request.ContractedDays
            .Select(d => new ContractedDay { Weekday = d.Weekday, StartTime = d.StartTime, EndTime = d.EndTime })
            .ToList();
        contract.DailyRateCents = request.DailyRateCents;
        contract.Consent = CreateContractCommandHandler.ToConsent(request.Consent);
        contract.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return ContractResult.Success(ContractMapper.ToResponse(contract));
    }
}
