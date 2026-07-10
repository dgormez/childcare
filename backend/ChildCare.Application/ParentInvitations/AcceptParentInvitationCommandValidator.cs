using FluentValidation;

namespace ChildCare.Application.ParentInvitations;

public class AcceptParentInvitationCommandValidator : AbstractValidator<AcceptParentInvitationCommand>
{
    public AcceptParentInvitationCommandValidator()
    {
        RuleFor(x => x.OrganisationSlug)
            .NotEmpty().WithMessage("errors.parent_invitation.organisation_slug_required");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("errors.parent_invitation.token_required");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.parent_invitation.password_required")
            .MinimumLength(8).WithMessage("errors.parent_invitation.password_too_short");
    }
}
