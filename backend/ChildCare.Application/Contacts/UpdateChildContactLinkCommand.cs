using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contacts;

public record UpdateChildContactLinkCommand(
    Guid ChildId,
    Guid ContactId,
    ContactRelationship Relationship,
    bool CanPickup,
    bool IsPrimary) : IRequest<ChildContactResult>;

public class UpdateChildContactLinkCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateChildContactLinkCommand, ChildContactResult>
{
    public async Task<ChildContactResult> Handle(UpdateChildContactLinkCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ChildContactResult.Fail(ContactFailure.ChildNotFound);

        var link = await db.ChildContacts.FirstOrDefaultAsync(
            cc => cc.ChildId == request.ChildId && cc.ContactId == request.ContactId, cancellationToken);
        if (link is null)
            return ChildContactResult.Fail(ContactFailure.NotFound);

        var contact = await db.Contacts.FirstAsync(c => c.Id == request.ContactId, cancellationToken);

        // FR-007: setting IsPrimary = true clears (not deletes) every other link's flag for
        // this child in the same transaction.
        if (request.IsPrimary && !link.IsPrimary)
        {
            var otherPrimaries = db.ChildContacts.Where(cc => cc.ChildId == request.ChildId && cc.ContactId != request.ContactId && cc.IsPrimary);
            await foreach (var other in otherPrimaries.AsAsyncEnumerable().WithCancellation(cancellationToken))
                other.IsPrimary = false;
        }

        link.Relationship = request.Relationship;
        link.CanPickup = request.CanPickup;
        link.IsPrimary = request.IsPrimary;

        await db.SaveChangesAsync(cancellationToken);

        return ChildContactResult.Success(ContactMapper.ToChildContactResponse(link, contact));
    }
}
