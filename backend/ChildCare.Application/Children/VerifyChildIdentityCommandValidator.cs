using FluentValidation;

namespace ChildCare.Application.Children;

public class VerifyChildIdentityCommandValidator : AbstractValidator<VerifyChildIdentityCommand>
{
    public VerifyChildIdentityCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotNull().WithMessage("errors.child.document_type_required");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("errors.child.identity_note_too_long");
    }
}
