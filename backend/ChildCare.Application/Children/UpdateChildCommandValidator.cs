using FluentValidation;

namespace ChildCare.Application.Children;

public class UpdateChildCommandValidator : AbstractValidator<UpdateChildCommand>
{
    public UpdateChildCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.child.firstname_required")
            .MaximumLength(100).WithMessage("errors.child.firstname_too_long");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.child.lastname_required")
            .MaximumLength(100).WithMessage("errors.child.lastname_too_long");

        RuleFor(x => x.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("errors.child.date_of_birth_in_future");

        RuleFor(x => x.Nationality).MaximumLength(100).WithMessage("errors.child.nationality_too_long");
        RuleFor(x => x.AllergiesDescription).MaximumLength(2000).WithMessage("errors.child.allergies_description_too_long");
        RuleFor(x => x.MedicalConditions).MaximumLength(2000).WithMessage("errors.child.medical_conditions_too_long");
        RuleFor(x => x.DietaryRestrictions).MaximumLength(2000).WithMessage("errors.child.dietary_restrictions_too_long");
        RuleFor(x => x.GpName).MaximumLength(200).WithMessage("errors.child.gp_name_too_long");
        RuleFor(x => x.GpPhone).MaximumLength(30).WithMessage("errors.child.gp_phone_too_long");
        RuleFor(x => x.PediatricianName).MaximumLength(200).WithMessage("errors.child.pediatrician_name_too_long");
        RuleFor(x => x.PediatricianPhone).MaximumLength(30).WithMessage("errors.child.pediatrician_phone_too_long");
        RuleFor(x => x.HealthInsuranceNumber).MaximumLength(50).WithMessage("errors.child.health_insurance_number_too_long");
        RuleFor(x => x.Kindcode).MaximumLength(20).WithMessage("errors.child.kindcode_too_long");
    }
}
