using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// FR-005: renames/re-categorizes. Per 013g FR-010 (re-verified here, not re-implemented), a
// VaccineRecord that already referenced this entry keeps its own originally-saved name text
// unchanged — this command only ever mutates the VaccineType row itself.
public record UpdateVaccineTypeCommand(Guid Id, string Name, string? Category) : IRequest<PlatformAdminVaccineTypeResult>;

public class UpdateVaccineTypeCommandValidator : AbstractValidator<UpdateVaccineTypeCommand>
{
    public UpdateVaccineTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category)
            .Must(c => string.IsNullOrEmpty(c) || VaccineCategoryExtensions.TryParseWireString(c, out _))
            .WithMessage("errors.validation");
    }
}

public class UpdateVaccineTypeCommandHandler(IPublicDbContext publicDb)
    : IRequestHandler<UpdateVaccineTypeCommand, PlatformAdminVaccineTypeResult>
{
    public async Task<PlatformAdminVaccineTypeResult> Handle(UpdateVaccineTypeCommand request, CancellationToken cancellationToken)
    {
        var entry = await publicDb.VaccineTypes.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        if (entry is null)
            return PlatformAdminVaccineTypeResult.Fail(PlatformAdminVaccineTypeFailure.NotFound);

        VaccineCategory? category = null;
        if (!string.IsNullOrEmpty(request.Category) && VaccineCategoryExtensions.TryParseWireString(request.Category, out var parsed))
            category = parsed;

        entry.Name = request.Name.Trim();
        entry.Category = category;
        entry.UpdatedAt = DateTime.UtcNow;

        await publicDb.SaveChangesAsync(cancellationToken);

        return PlatformAdminVaccineTypeResult.Success(PlatformAdminVaccineTypeMapper.ToResponse(entry));
    }
}
