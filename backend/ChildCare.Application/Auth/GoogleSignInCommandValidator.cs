using FluentValidation;

namespace ChildCare.Application.Auth;

public class GoogleSignInCommandValidator : AbstractValidator<GoogleSignInCommand>
{
    public GoogleSignInCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("errors.auth.id_token_required");
    }
}
