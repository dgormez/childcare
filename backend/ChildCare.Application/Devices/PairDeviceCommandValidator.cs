using FluentValidation;

namespace ChildCare.Application.Devices;

public class PairDeviceCommandValidator : AbstractValidator<PairDeviceCommand>
{
    public PairDeviceCommandValidator()
    {
        RuleFor(x => x.LocationId)
            .NotEmpty().WithMessage("errors.devices.location_required");

        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("errors.devices.group_required");

        RuleFor(x => x.DirectorOverridePin)
            .NotEmpty().WithMessage("errors.devices.override_pin_required")
            .Matches("^[0-9]{6}$").WithMessage("errors.devices.override_pin_invalid_format");
    }
}
