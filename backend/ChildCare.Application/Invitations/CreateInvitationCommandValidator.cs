using FluentValidation;

namespace ChildCare.Application.Invitations;

public class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("errors.invitation.email_required")
            .EmailAddress().WithMessage("errors.invitation.email_invalid");
    }
}
