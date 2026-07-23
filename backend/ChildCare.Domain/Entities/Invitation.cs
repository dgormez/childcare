namespace ChildCare.Domain.Entities;

public class Invitation
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public string   Email       { get; set; } = string.Empty;
    public byte[]   TokenHash   { get; set; } = [];
    public DateTime ExpiresAt   { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    // No UsedAt column: "used" is derived from whether a ready Tenant exists
    // with CreatedFromInvitationId pointing at this invitation (research.md R10).

    // Feature 032 — platform-admin invitation portal (data-model.md, research.md R1/R12/R14).
    // OrganisationNameNote is informational only, never authoritative (spec.md FR-001/Assumptions).
    // Locale drives the invitation email's language (research.md R9).
    public string? OrganisationNameNote { get; set; }
    public string  Locale               { get; set; } = "nl";

    // Creation attribution (FR-008). Nullable so feature 001's pre-existing rows (created
    // before this column existed) need no backfill (research.md R12) — a null creator on a
    // pre-feature row is accurate, not a data-quality defect. No DB-level FK on CreatedByUserId:
    // the acting TenantUser lives in an arbitrary tenant schema, which Postgres cannot
    // cross-schema FK-enforce (same posture as VaccineType.DeactivatedByUserId, feature 013h).
    public Guid?    CreatedByUserId  { get; set; }
    public string?  CreatedByEmail   { get; set; }

    // Revoke attribution — shared by both an explicit platform-admin "Revoke" action and a
    // resend/duplicate-create-triggered supersede (spec.md Clarifications, research.md R3): the
    // data model makes no distinction between the two triggers, so there is one set of fields,
    // not two. Invariant: all three null, or all three populated together — never partial.
    public Guid?     RevokedByUserId { get; set; }
    public string?   RevokedByEmail  { get; set; }
    public DateTime? RevokedAt       { get; set; }
}
