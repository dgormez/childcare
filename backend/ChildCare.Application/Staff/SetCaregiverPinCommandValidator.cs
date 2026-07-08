using FluentValidation;

namespace ChildCare.Application.Staff;

public class SetCaregiverPinCommandValidator : AbstractValidator<SetCaregiverPinCommand>
{
    public SetCaregiverPinCommandValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("errors.pin.required")
            .Matches("^[0-9]{4}$").WithMessage("errors.pin.invalid_format");
    }
}
