using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contacts;

public record UnlinkContactFromChildCommand(Guid ChildId, Guid ContactId) : IRequest<ContactResult>;

public class UnlinkContactFromChildCommandHandler(ITenantDbContext db) : IRequestHandler<UnlinkContactFromChildCommand, ContactResult>
{
    public async Task<ContactResult> Handle(UnlinkContactFromChildCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ContactResult.Fail(ContactFailure.ChildNotFound);

        var link = await db.ChildContacts.FirstOrDefaultAsync(
            cc => cc.ChildId == request.ChildId && cc.ContactId == request.ContactId, cancellationToken);

        if (link is not null)
        {
            var wasPrimary = link.IsPrimary;
            db.ChildContacts.Remove(link);
            await db.SaveChangesAsync(cancellationToken);

            // FR-007, /speckit-checklist CHK005: if the removed link was primary and other
            // links remain, promote the most-recently-linked remaining one — the "exactly one
            // primary whenever >=1 contact exists" invariant must never be silently violated.
            if (wasPrimary)
            {
                var replacement = await db.ChildContacts
                    .Where(cc => cc.ChildId == request.ChildId)
                    .OrderByDescending(cc => cc.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (replacement is not null)
                {
                    replacement.IsPrimary = true;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        return ContactResult.Success();
    }
}
