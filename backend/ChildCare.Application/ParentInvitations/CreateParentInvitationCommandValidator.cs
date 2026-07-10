using ChildCare.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ParentInvitations;

public class CreateParentInvitationCommandValidator : AbstractValidator<CreateParentInvitationCommand>
{
    public CreateParentInvitationCommandValidator(ITenantDbContext db)
    {
        // FR-000a: eligible = contact exists, has an email on file, and has at least one
        // ChildContact link with CanPickup = true. A non-existent contact is trivially
        // "not eligible" too — no separate not-found error taxonomy for a stale client reference.
        RuleFor(x => x.ContactId)
            .MustAsync(async (contactId, ct) =>
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == contactId, ct);
                if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
                    return false;

                return await db.ChildContacts.AnyAsync(cc => cc.ContactId == contactId && cc.CanPickup, ct);
            })
            .WithMessage("errors.parent_invitation.not_eligible");

        // "Already has an account" is deliberately NOT validated here — it needs a distinct 409
        // Conflict, not FluentValidation's generic 422 (research: every ValidationException maps
        // to 422 errors.validation, per Program.cs's global exception handler). Checked in
        // CreateParentInvitationCommandHandler instead, mirroring StaffFailure.EmailAlreadyExists'
        // precedent (feature 005) for the same reason.
    }
}
