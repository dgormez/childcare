using FluentValidation;

namespace ChildCare.Application.Contacts;

public class VerifyContactIdentityCommandValidator : AbstractValidator<VerifyContactIdentityCommand>
{
    public VerifyContactIdentityCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotNull().WithMessage("errors.contact.document_type_required");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("errors.contact.identity_note_too_long");
    }
}
