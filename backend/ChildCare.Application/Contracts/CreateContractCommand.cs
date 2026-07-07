using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using ChildCare.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

public record CreateContractCommand(
    Guid ChildId,
    Guid LocationId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayRequest> ContractedDays,
    int DailyRateCents,
    ContractConsentRequest? Consent) : IRequest<ContractResult>;

public class CreateContractCommandValidator : AbstractValidator<CreateContractCommand>
{
    private static readonly DayOfWeek[] ValidWeekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public CreateContractCommandValidator()
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

public class CreateContractCommandHandler(ITenantDbContext db) : IRequestHandler<CreateContractCommand, ContractResult>
{
    public async Task<ContractResult> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return ContractResult.Fail(ContractFailure.ChildNotFound);

        // FR-004a: a contract cannot be newly created against an already-deactivated location
        // (mirrors feature 006's CreateGroupCommand / CHK003 precedent).
        var locationActive = await db.Locations.AnyAsync(
            l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationActive)
            return ContractResult.Fail(ContractFailure.LocationNotFound);

        var contract = new Contract
        {
            ChildId = request.ChildId,
            LocationId = request.LocationId,
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate,
            ContractedDays = request.ContractedDays
                .Select(d => new ContractedDay { Weekday = d.Weekday, StartTime = d.StartTime, EndTime = d.EndTime })
                .ToList(),
            DailyRateCents = request.DailyRateCents,
            Status = ContractStatus.Draft,
            Consent = ToConsent(request.Consent),
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        return ContractResult.Success(ContractMapper.ToResponse(contract));
    }

    // FR-010: any flag not explicitly true — including an entirely omitted consent object —
    // defaults to false. Consent for photographing/filming minors is never inferred.
    internal static ContractConsent ToConsent(ContractConsentRequest? request) => new()
    {
        PhotosInternal = request?.PhotosInternal ?? false,
        PhotosWebsite = request?.PhotosWebsite ?? false,
        PhotosSocialMedia = request?.PhotosSocialMedia ?? false,
        VideoInternal = request?.VideoInternal ?? false,
        PhotosPress = request?.PhotosPress ?? false,
    };
}
