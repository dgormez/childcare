using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

public record UpdateVaccineRecordCommand(
    Guid ChildId,
    Guid Id,
    string VaccineName,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes) : IRequest<VaccineRecordResult>;

public class UpdateVaccineRecordCommandValidator : AbstractValidator<UpdateVaccineRecordCommand>
{
    public UpdateVaccineRecordCommandValidator()
    {
        RuleFor(x => x.VaccineName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.vaccine_records.vaccine_name_required")
            .MaximumLength(200).WithMessage("errors.vaccine_records.vaccine_name_too_long");

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

public class UpdateVaccineRecordCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateVaccineRecordCommand, VaccineRecordResult>
{
    public async Task<VaccineRecordResult> Handle(UpdateVaccineRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.VaccineRecords
            .SingleOrDefaultAsync(v => v.Id == request.Id && v.ChildId == request.ChildId && v.DeletedAt == null, cancellationToken);
        if (record is null)
            return VaccineRecordResult.Fail(VaccineRecordFailure.NotFound);

        record.VaccineName = request.VaccineName;
        record.DoseNumber = request.DoseNumber;
        record.AdministeredOn = request.AdministeredOn;
        record.NextDueDate = request.NextDueDate;
        record.AdministeredBy = request.AdministeredBy;
        record.Notes = request.Notes;
        record.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return VaccineRecordResult.Success(VaccineRecordMapper.ToResponse(record));
    }
}
