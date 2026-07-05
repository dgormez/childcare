using FluentValidation;

namespace ChildCare.Application.Auth;

public class AppleSignInCommandValidator : AbstractValidator<AppleSignInCommand>
{
    public AppleSignInCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.IdentityToken)
            .NotEmpty().WithMessage("errors.auth.identity_token_required");
    }
}
