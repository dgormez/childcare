using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Contacts;

internal static class ContactMapper
{
    public static ContactResponse ToResponse(Contact c) => new(
        c.Id, c.FirstName, c.LastName, c.Phone, c.Email, c.Locale);

    public static ChildContactResponse ToChildContactResponse(ChildContact link, Contact contact) => new(
        contact.Id, contact.FirstName, contact.LastName, contact.Phone, contact.Email, contact.Locale,
        link.Relationship.ToString(), link.CanPickup, link.IsPrimary);
}
