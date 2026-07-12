using ChildCare.Application.Common;
using FluentValidation;

namespace ChildCare.Application.Locations;

public class UpdateLocationReservationSettingsCommandValidator : AbstractValidator<UpdateLocationReservationSettingsCommand>
{
    public UpdateLocationReservationSettingsCommandValidator()
    {
        RuleFor(x => x.AbsencesMode).Must(m => ReservationModeMapper.TryParse(m, out _)).WithMessage("errors.location.reservation_settings.invalid_mode");
        RuleFor(x => x.ExtrasMode).Must(m => ReservationModeMapper.TryParse(m, out _)).WithMessage("errors.location.reservation_settings.invalid_mode");
        RuleFor(x => x.SwapsMode).Must(m => ReservationModeMapper.TryParse(m, out _)).WithMessage("errors.location.reservation_settings.invalid_mode");

        // FR-011: a sane defensive ceiling (one year), not a product requirement — see spec.md
        // Assumptions.
        RuleFor(x => x.NoticeHours).InclusiveBetween(0, 8760).WithMessage("errors.location.reservation_settings.notice_hours_out_of_range");
    }
}
