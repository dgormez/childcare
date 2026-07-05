using FluentValidation;

namespace ChildCare.Application.Auth;

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("errors.auth.token_required");
    }
}
