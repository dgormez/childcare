using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ChildCare.Application.Contacts;

public record CreateContactCommand(
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale) : IRequest<ContactResult>;

public class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.contact.firstname_required")
            .MaximumLength(100).WithMessage("errors.contact.firstname_too_long");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.contact.lastname_required")
            .MaximumLength(100).WithMessage("errors.contact.lastname_too_long");

        RuleFor(x => x.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.contact.phone_required")
            .MaximumLength(30).WithMessage("errors.contact.phone_too_long");

        RuleFor(x => x.Email)
            .MaximumLength(254).WithMessage("errors.contact.email_too_long")
            .EmailAddress().WithMessage("errors.contact.email_invalid")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.contact.locale_required")
            .MaximumLength(5).WithMessage("errors.contact.locale_too_long");
    }
}

public class CreateContactCommandHandler(ITenantDbContext db) : IRequestHandler<CreateContactCommand, ContactResult>
{
    public async Task<ContactResult> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = new Contact
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Email = request.Email,
            Locale = request.Locale,
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);

        return ContactResult.Success(ContactMapper.ToResponse(contact));
    }
}
