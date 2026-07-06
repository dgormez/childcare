using FluentValidation;

namespace ChildCare.Application.Staff;

public class AcceptStaffInvitationCommandValidator : AbstractValidator<AcceptStaffInvitationCommand>
{
    public AcceptStaffInvitationCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.staff.organisation_slug_required");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("errors.staff.token_required");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.password_required")
            .MinimumLength(8).WithMessage("errors.staff.password_too_short");
    }
}
