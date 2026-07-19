namespace ChildCare.Domain.Enums;

public enum ContactRelationship
{
    Mother,
    Father,
    Guardian,
    EmergencyContact,
    AuthorisedPickup,

    // Feature 030 (spec.md FR-014) — appended, not inserted, since this enum has no explicit EF
    // HasConversion (plain integer column) and existing stored values must never be renumbered.
    FosterParent,
    Other,
}
