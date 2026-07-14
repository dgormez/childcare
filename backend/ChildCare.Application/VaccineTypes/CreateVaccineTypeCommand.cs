using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// contracts/platform-admin-vaccine-types-api.md — SortOrder defaults to max(existing)+1, IsActive
// defaults to true (data-model.md).
public record CreateVaccineTypeCommand(string Name, string? Category) : IRequest<PlatformAdminVaccineTypeResult>;

public class CreateVaccineTypeCommandValidator : AbstractValidator<CreateVaccineTypeCommand>
{
    public CreateVaccineTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category)
            .Must(c => string.IsNullOrEmpty(c) || VaccineCategoryExtensions.TryParseWireString(c, out _))
            .WithMessage("errors.validation");
    }
}

public class CreateVaccineTypeCommandHandler(IPublicDbContext publicDb)
    : IRequestHandler<CreateVaccineTypeCommand, PlatformAdminVaccineTypeResult>
{
    public async Task<PlatformAdminVaccineTypeResult> Handle(CreateVaccineTypeCommand request, CancellationToken cancellationToken)
    {
        VaccineCategory? category = null;
        if (!string.IsNullOrEmpty(request.Category) && VaccineCategoryExtensions.TryParseWireString(request.Category, out var parsed))
            category = parsed;

        var maxSortOrder = await publicDb.VaccineTypes
            .Select(v => (int?)v.SortOrder)
            .MaxAsync(cancellationToken);

        var entry = new VaccineType
        {
            Name = request.Name.Trim(),
            Category = category,
            SortOrder = (maxSortOrder ?? -1) + 1,
            IsActive = true,
        };

        publicDb.VaccineTypes.Add(entry);
        await publicDb.SaveChangesAsync(cancellationToken);

        return PlatformAdminVaccineTypeResult.Success(PlatformAdminVaccineTypeMapper.ToResponse(entry));
    }
}
