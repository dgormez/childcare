using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class Child
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public string    FirstName   { get; set; } = string.Empty;
    public string    LastName    { get; set; } = string.Empty;

    // Nullable: a child can be registered before birth (or before the parent knows the exact
    // date), so onboarding only requires a name — everything else, including this, is added later.
    public DateOnly? DateOfBirth { get; set; }

    // GCS object path only, never a URL (research.md R1) — signed download URLs are generated
    // fresh on every read.
    public string? ProfilePhotoObjectPath { get; set; }

    public Gender? Gender      { get; set; }
    public string? Nationality { get; set; }

    // Medical information — all nullable, never blocks file creation (FR-003).
    public string?          AllergiesDescription  { get; set; }
    public AllergySeverity? AllergySeverity        { get; set; }
    public string?          MedicalConditions      { get; set; }
    public string?          DietaryRestrictions    { get; set; }
    public string?          PediatricianName       { get; set; }
    public string?          PediatricianPhone      { get; set; }
    public string?          HealthInsuranceNumber  { get; set; }

    // Opgroeien child identifier (YYMMDD-NNN) — stored as entered, not format-validated in
    // Phase 1 (FR-009).
    public string? Kindcode { get; set; }

    // Feature 022: identity-verification audit trail. IdVerifiedAt/IdDocumentType together form
    // the "is this verified" state (spec.md FR-003); *ByUserId has no DB-level FK (attribution
    // field, not a queried relationship — mirrors VaccineType.DeactivatedByUserId, 013h).
    // First* is set once and never overwritten by a later correction (FR-006).
    public DateTime?       IdVerifiedAt            { get; set; }
    public Guid?           IdVerifiedByUserId      { get; set; }
    public string?         IdVerifiedByEmail       { get; set; }
    public IdDocumentType? IdDocumentType          { get; set; }
    public string?         IdDocumentNote          { get; set; }
    public DateTime?       FirstIdVerifiedAt       { get; set; }
    public Guid?           FirstIdVerifiedByUserId { get; set; }
    public string?         FirstIdVerifiedByEmail  { get; set; }

    // Belgian National Register Number — encrypted at rest (ASP.NET Core Data Protection,
    // research.md R3). NrnLast4 is plaintext, computed once at write time, never derived by
    // decrypting EncryptedNrn (FR-011/FR-012). NrnHash is a deterministic SHA-256 of the
    // normalized number — EncryptedNrn uses a random IV per write so it can't be compared for
    // uniqueness directly; NrnHash exists solely so the DB can enforce "one NRN per child".
    public string? EncryptedNrn { get; set; }
    public string? NrnLast4     { get; set; }
    public string? NrnHash      { get; set; }

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
