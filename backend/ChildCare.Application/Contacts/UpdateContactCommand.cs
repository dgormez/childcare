using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contacts;

public record UpdateContactCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale) : IRequest<ContactResult>;

public class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator()
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

public class UpdateContactCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateContactCommand, ContactResult>
{
    public async Task<ContactResult> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contact is null)
            return ContactResult.Fail(ContactFailure.NotFound);

        contact.FirstName = request.FirstName;
        contact.LastName = request.LastName;
        contact.Phone = request.Phone;
        contact.Email = request.Email;
        contact.Locale = request.Locale;
        contact.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return ContactResult.Success(ContactMapper.ToResponse(contact));
    }
}
