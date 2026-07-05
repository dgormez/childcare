using FluentValidation;

namespace ChildCare.Application.Auth;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("errors.auth.refresh_token_required");
    }
}
