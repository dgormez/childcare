using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// Join entity — composite PK (ChildId, ContactId), one relationship per pair (research.md R3,
// revised during /speckit-analyze to keep PUT/DELETE routes on this pair unambiguous).
public class ChildContact
{
    public Guid                ChildId      { get; set; }
    public Guid                ContactId    { get; set; }
    public ContactRelationship Relationship { get; set; }
    public bool                CanPickup    { get; set; }

    // At most one true per ChildId, enforced in the Application layer (FR-007) — the first
    // link ever created for a child defaults to true.
    public bool     IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
