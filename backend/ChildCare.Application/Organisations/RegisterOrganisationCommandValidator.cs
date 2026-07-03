using FluentValidation;

namespace ChildCare.Application.Organisations;

/// <summary>
/// Field-shape rules only — all map to 422 via the ValidationBehavior pipeline. Whether the
/// invitation itself is resolvable (→ 404) and whether the email matches the invitation's
/// target email (→ 422 with a different envelope) both depend on resolving the invitation
/// first, so those live in RegisterOrganisationCommandHandler instead (FR-004/FR-018 —
/// tasks.md T044/T045; see the handler for why this isn't split across both layers).
/// </summary>
public class RegisterOrganisationCommandValidator : AbstractValidator<RegisterOrganisationCommand>
{
    public RegisterOrganisationCommandValidator()
    {
        RuleFor(x => x.InvitationToken)
            .NotEmpty().WithMessage("errors.registration.invitation_token_required");

        RuleFor(x => x.OrganisationName)
            .NotEmpty().WithMessage("errors.registration.organisation_name_required");

        RuleFor(x => x.DirectorName)
            .NotEmpty().WithMessage("errors.registration.director_name_required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("errors.registration.email_required")
            .EmailAddress().WithMessage("errors.registration.email_invalid");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("errors.registration.password_required")
            .MinimumLength(8).WithMessage("errors.registration.password_too_short");
    }
}
