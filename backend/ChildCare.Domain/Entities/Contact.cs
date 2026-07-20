using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// Standalone person record. Never deleted; a contact simply accumulates/loses ChildContact
// links over time.
public class Contact
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public string Phone     { get; set; } = string.Empty;
    public string? Email    { get; set; }
    public string Locale    { get; set; } = "nl";

    // Feature 022: identity-verification audit trail — same shape as Child's, independent of
    // which children this contact is linked to (spec.md FR-002). See Child.cs for field-by-field
    // reasoning.
    public DateTime?       IdVerifiedAt            { get; set; }
    public Guid?           IdVerifiedByUserId      { get; set; }
    public string?         IdVerifiedByEmail       { get; set; }
    public IdDocumentType? IdDocumentType          { get; set; }
    public string?         IdDocumentNote          { get; set; }
    public DateTime?       FirstIdVerifiedAt       { get; set; }
    public Guid?           FirstIdVerifiedByUserId { get; set; }
    public string?         FirstIdVerifiedByEmail  { get; set; }

    // Feature 013: set once this contact accepts a director-issued parent-app invitation
    // (ParentInvitation). Null = no parent account exists yet. One Contact <-> at most one
    // TenantUser(Role=Parent) — see specs/013-parent-communication/research.md R1.
    public Guid? TenantUserId { get; set; }

    // Expo push token (feature 009) — nullable, never populated by any client yet since no
    // parent-facing registration path exists (accepted gap, spec.md Assumptions). Exists now so
    // the temperature-alert recipient query is real/testable ahead of that registration UI.
    public string? PushToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Feature 020: null = subscribed (default). Set = unsubscribed from the automatic daily
    // digest, timestamped for audit. Never affects bulk/announcement/closure emails or an
    // on-demand resend — those channels always deliver regardless of this flag (spec.md FR-008).
    public DateTime? DigestUnsubscribedAt { get; set; }
}
