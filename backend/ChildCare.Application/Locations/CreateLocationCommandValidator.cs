using FluentValidation;

namespace ChildCare.Application.Locations;

public class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
    // Permissive international format: digits with optional leading +, spaces, hyphens,
    // parentheses (spec.md Assumptions: "phone number with optional country code", no
    // Belgium-specific format mandated).
    internal const string PhonePattern = @"^\+?[0-9\s\-()]{6,30}$";

    public CreateLocationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("errors.location.name_required")
            .MaximumLength(200).WithMessage("errors.location.name_too_long");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("errors.location.address_required")
            .MaximumLength(500).WithMessage("errors.location.address_too_long");

        RuleFor(x => x.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.location.phone_required")
            .MaximumLength(30).WithMessage("errors.location.phone_too_long")
            .Matches(PhonePattern).WithMessage("errors.location.phone_invalid");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.location.email_required")
            .MaximumLength(254).WithMessage("errors.location.email_too_long")
            .EmailAddress().WithMessage("errors.location.email_invalid");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).WithMessage("errors.location.max_capacity_invalid");
    }
}
