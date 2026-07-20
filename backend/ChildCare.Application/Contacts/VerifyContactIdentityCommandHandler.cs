using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contacts;

public class VerifyContactIdentityCommandHandler(ITenantDbContext db)
    : IRequestHandler<VerifyContactIdentityCommand, ContactResult>
{
    public async Task<ContactResult> Handle(VerifyContactIdentityCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken);
        if (contact is null)
            return ContactResult.Fail(ContactFailure.NotFound);

        var now = DateTime.UtcNow;

        contact.IdVerifiedAt = now;
        contact.IdVerifiedByUserId = request.VerifiedByUserId;
        contact.IdVerifiedByEmail = request.VerifiedByEmail;
        contact.IdDocumentType = request.DocumentType;
        contact.IdDocumentNote = request.Note;

        // FR-006: the first verification is set once and never overwritten by a later correction.
        if (contact.FirstIdVerifiedAt is null)
        {
            contact.FirstIdVerifiedAt = now;
            contact.FirstIdVerifiedByUserId = request.VerifiedByUserId;
            contact.FirstIdVerifiedByEmail = request.VerifiedByEmail;
        }

        contact.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        return ContactResult.Success(ContactMapper.ToResponse(contact));
    }
}
