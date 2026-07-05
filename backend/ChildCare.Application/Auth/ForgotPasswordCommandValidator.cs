using FluentValidation;

namespace ChildCare.Application.Auth;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("errors.auth.email_required")
            .EmailAddress().WithMessage("errors.auth.email_invalid");
    }
}
