using FluentValidation;

namespace ChildCare.Application.Auth;

/// <summary>Field-shape rules only — whether the organisation/credentials actually resolve
/// (→ 404/401) lives in LoginCommandHandler, not here (mirrors RegisterOrganisationCommandValidator).</summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.auth.organisation_slug_required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("errors.auth.email_required")
            .EmailAddress().WithMessage("errors.auth.email_invalid");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("errors.auth.password_required");
    }
}
