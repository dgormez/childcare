using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferences;

// UpdatedBy comes from the caller's JWT (endpoint layer resolves it) — never client-supplied.
// All fields nullable: null means "no change" on update (mirrors CorrectAttendanceRecordCommand's
// null-coalesce merge pattern) or "use the column default" on first creation (data-model.md).
public record UpsertMealPreferenceCommand(
    Guid ChildId,
    string? Texture,
    List<string>? DietaryType,
    string? PortionSize,
    string? AdditionalNotes,
    Guid UpdatedBy) : IRequest<MealPreferenceResult>;

public class UpsertMealPreferenceCommandValidator : AbstractValidator<UpsertMealPreferenceCommand>
{
    public UpsertMealPreferenceCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();

        RuleFor(x => x.Texture)
            .Must(v => v is null || Enum.TryParse<MealTexture>(v, ignoreCase: true, out _))
            .WithMessage("errors.meal_preferences.texture_invalid");

        RuleFor(x => x.PortionSize)
            .Must(v => v is null || Enum.TryParse<MealPortionSize>(v, ignoreCase: true, out _))
            .WithMessage("errors.meal_preferences.portion_size_invalid");

        RuleFor(x => x.DietaryType)
            .Must(v => v is null || v.All(d => DietaryTypeExtensions.TryParseWireString(d, out _)))
            .WithMessage("errors.meal_preferences.dietary_type_invalid");

        RuleFor(x => x.AdditionalNotes)
            .MaximumLength(2000)
            .WithMessage("errors.meal_preferences.additional_notes_too_long");
    }
}

public class UpsertMealPreferenceCommandHandler(ITenantDbContext db) : IRequestHandler<UpsertMealPreferenceCommand, MealPreferenceResult>
{
    public async Task<MealPreferenceResult> Handle(UpsertMealPreferenceCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId && c.DeactivatedAt == null, cancellationToken);
        if (!childExists)
            return MealPreferenceResult.Fail(MealPreferenceFailure.ChildNotFound);

        var preference = await db.MealPreferences.FirstOrDefaultAsync(p => p.ChildId == request.ChildId, cancellationToken);
        if (preference is null)
        {
            preference = new MealPreference { ChildId = request.ChildId };
            db.MealPreferences.Add(preference);
        }

        if (request.Texture is not null)
            preference.Texture = Enum.Parse<MealTexture>(request.Texture, ignoreCase: true);

        if (request.PortionSize is not null)
            preference.PortionSize = Enum.Parse<MealPortionSize>(request.PortionSize, ignoreCase: true);

        if (request.DietaryType is not null)
        {
            preference.DietaryType = request.DietaryType
                .Select(d => DietaryTypeExtensions.TryParseWireString(d, out var parsed) ? parsed : default)
                .ToList();
        }

        if (request.AdditionalNotes is not null)
            preference.AdditionalNotes = request.AdditionalNotes;

        preference.UpdatedBy = request.UpdatedBy;
        preference.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return MealPreferenceResult.Success(MealListMapper.ToPreferenceResponse(preference));
    }
}
