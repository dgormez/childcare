using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

public record RecordVaccinationCommand(Guid ChildId, string VaccineName, DateOnly DateAdministered, DateOnly? NextDueDate)
    : IRequest<VaccinationResult>;

public class RecordVaccinationCommandValidator : AbstractValidator<RecordVaccinationCommand>
{
    public RecordVaccinationCommandValidator()
    {
        RuleFor(x => x.VaccineName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.vaccination.vaccine_name_required")
            .MaximumLength(200).WithMessage("errors.vaccination.vaccine_name_too_long");

        // /speckit-checklist CHK002: administered date must not be in the future.
        RuleFor(x => x.DateAdministered)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("errors.vaccination.date_administered_in_future");
    }
}

public class RecordVaccinationCommandHandler(ITenantDbContext db) : IRequestHandler<RecordVaccinationCommand, VaccinationResult>
{
    public async Task<VaccinationResult> Handle(RecordVaccinationCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return VaccinationResult.Fail(GroupFailure.ChildNotFound);

        var record = new VaccinationRecord
        {
            ChildId = request.ChildId,
            VaccineName = request.VaccineName,
            DateAdministered = request.DateAdministered,
            NextDueDate = request.NextDueDate,
        };

        db.VaccinationRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return VaccinationResult.Success(GroupMapper.ToVaccinationResponse(record));
    }
}
