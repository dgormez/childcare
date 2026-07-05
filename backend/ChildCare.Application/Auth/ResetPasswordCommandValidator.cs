using FluentValidation;

namespace ChildCare.Application.Auth;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("errors.auth.token_required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("errors.auth.password_required")
            .MinimumLength(8).WithMessage("errors.auth.password_too_short");
    }
}
