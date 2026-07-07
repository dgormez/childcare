using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contacts;

public record LinkContactToChildCommand(
    Guid ChildId,
    Guid ContactId,
    ContactRelationship Relationship,
    bool CanPickup,
    bool IsPrimary) : IRequest<ChildContactResult>;

public class LinkContactToChildCommandValidator : AbstractValidator<LinkContactToChildCommand>
{
    public LinkContactToChildCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("errors.contact.contact_id_required");
    }
}

public class LinkContactToChildCommandHandler(ITenantDbContext db) : IRequestHandler<LinkContactToChildCommand, ChildContactResult>
{
    public async Task<ChildContactResult> Handle(LinkContactToChildCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ChildContactResult.Fail(ContactFailure.ChildNotFound);

        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken);
        if (contact is null)
            return ChildContactResult.Fail(ContactFailure.NotFound);

        var alreadyLinked = await db.ChildContacts.AnyAsync(
            cc => cc.ChildId == request.ChildId && cc.ContactId == request.ContactId, cancellationToken);
        if (alreadyLinked)
            return ChildContactResult.Fail(ContactFailure.LinkAlreadyExists);

        // FR-007: the first-ever contact link for a child is always primary, regardless of the
        // request value.
        var hasAnyLink = await db.ChildContacts.AnyAsync(cc => cc.ChildId == request.ChildId, cancellationToken);
        var isPrimary = !hasAnyLink || request.IsPrimary;

        if (isPrimary && hasAnyLink)
        {
            var existingPrimaries = db.ChildContacts.Where(cc => cc.ChildId == request.ChildId && cc.IsPrimary);
            await foreach (var existing in existingPrimaries.AsAsyncEnumerable().WithCancellation(cancellationToken))
                existing.IsPrimary = false;
        }

        var link = new ChildContact
        {
            ChildId = request.ChildId,
            ContactId = request.ContactId,
            Relationship = request.Relationship,
            CanPickup = request.CanPickup,
            IsPrimary = isPrimary,
        };

        db.ChildContacts.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        return ChildContactResult.Success(ContactMapper.ToChildContactResponse(link, contact));
    }
}
