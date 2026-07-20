using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.Contacts;

internal static class ContactMapper
{
    // No role-gating here (unlike ChildMapper) — every contact-reading route is already
    // DirectorOnly (research.md R8), so there's no non-Director caller to gate against.
    public static ContactResponse ToResponse(Contact c) => new(
        c.Id, c.FirstName, c.LastName, c.Phone, c.Email, c.Locale,
        c.IdVerifiedAt, c.IdVerifiedByEmail, c.IdDocumentType?.ToWireString(), c.IdDocumentNote,
        c.FirstIdVerifiedAt, c.FirstIdVerifiedByEmail);

    public static ChildContactResponse ToChildContactResponse(ChildContact link, Contact contact) => new(
        contact.Id, contact.FirstName, contact.LastName, contact.Phone, contact.Email, contact.Locale,
        link.Relationship.ToString(), link.CanPickup, link.IsPrimary,
        contact.IdVerifiedAt, contact.IdVerifiedByEmail, contact.IdDocumentType?.ToWireString(), contact.IdDocumentNote,
        contact.FirstIdVerifiedAt, contact.FirstIdVerifiedByEmail);
}
