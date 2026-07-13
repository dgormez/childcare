using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

// RecordedBy comes from the caller's JWT (endpoint layer resolves it) — never client-supplied.
public record CreateVaccineRecordCommand(
    Guid ChildId,
    string VaccineName,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes,
    Guid RecordedBy) : IRequest<VaccineRecordResult>;

public class CreateVaccineRecordCommandValidator : AbstractValidator<CreateVaccineRecordCommand>
{
    public CreateVaccineRecordCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();

        RuleFor(x => x.VaccineName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.vaccine_records.vaccine_name_required")
            .MaximumLength(200).WithMessage("errors.vaccine_records.vaccine_name_too_long");

        // FR-001: administered date required, must not be in the future.
        RuleFor(x => x.AdministeredOn)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("errors.vaccine_records.administered_on_in_future");

        RuleFor(x => x.DoseNumber)
            .GreaterThanOrEqualTo(1)
            .When(x => x.DoseNumber.HasValue)
            .WithMessage("errors.vaccine_records.dose_number_invalid");

        RuleFor(x => x.AdministeredBy).MaximumLength(200).WithMessage("errors.vaccine_records.administered_by_too_long");
        RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("errors.vaccine_records.notes_too_long");
    }
}

public class CreateVaccineRecordCommandHandler(ITenantDbContext db) : IRequestHandler<CreateVaccineRecordCommand, VaccineRecordResult>
{
    public async Task<VaccineRecordResult> Handle(CreateVaccineRecordCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return VaccineRecordResult.Fail(VaccineRecordFailure.ChildNotFound);

        var record = new VaccineRecord
        {
            ChildId = request.ChildId,
            VaccineName = request.VaccineName,
            DoseNumber = request.DoseNumber,
            AdministeredOn = request.AdministeredOn,
            NextDueDate = request.NextDueDate,
            AdministeredBy = request.AdministeredBy,
            Notes = request.Notes,
            RecordedBy = request.RecordedBy,
        };

        db.VaccineRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return VaccineRecordResult.Success(VaccineRecordMapper.ToResponse(record));
    }
}
