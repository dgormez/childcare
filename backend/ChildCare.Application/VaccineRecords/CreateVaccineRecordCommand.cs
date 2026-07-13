using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

// RecordedBy comes from the caller's JWT (endpoint layer resolves it) — never client-supplied.
// VaccineTypeId (feature 013g) references the shared public-schema catalog — no DB FK
// (research.md R2), validated here instead. When null and VaccineName matches no active catalog
// entry, CustomVaccineEntryResolver (spec.md FR-006/FR-007) resolves/creates a tenant-scoped
// remembered entry — the two references are mutually exclusive (FR-004).
public record CreateVaccineRecordCommand(
    Guid ChildId,
    string VaccineName,
    Guid? VaccineTypeId,
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

public class CreateVaccineRecordCommandHandler(ITenantDbContext db, IPublicDbContext publicDb, IHealthAttachmentStorage storage) : IRequestHandler<CreateVaccineRecordCommand, VaccineRecordResult>
{
    public async Task<VaccineRecordResult> Handle(CreateVaccineRecordCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return VaccineRecordResult.Fail(VaccineRecordFailure.ChildNotFound);

        if (request.VaccineTypeId.HasValue)
        {
            // Any catalog row (active or not) is accepted — a deactivated entry can still be
            // legitimately referenced when editing/re-saving an older record (spec.md FR-010).
            var vaccineTypeExists = await publicDb.VaccineTypes.AnyAsync(v => v.Id == request.VaccineTypeId.Value, cancellationToken);
            if (!vaccineTypeExists)
                return VaccineRecordResult.Fail(VaccineRecordFailure.VaccineTypeNotFound);
        }

        Guid? customVaccineEntryId = request.VaccineTypeId is null
            ? await CustomVaccineEntryResolver.ResolveAsync(db, request.VaccineName, cancellationToken)
            : null;

        var record = new VaccineRecord
        {
            ChildId = request.ChildId,
            VaccineName = request.VaccineName,
            VaccineTypeId = request.VaccineTypeId,
            CustomVaccineEntryId = customVaccineEntryId,
            DoseNumber = request.DoseNumber,
            AdministeredOn = request.AdministeredOn,
            NextDueDate = request.NextDueDate,
            AdministeredBy = request.AdministeredBy,
            Notes = request.Notes,
            RecordedBy = request.RecordedBy,
        };

        db.VaccineRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return VaccineRecordResult.Success(await VaccineRecordMapper.ToResponseAsync(record, storage, cancellationToken));
    }
}
