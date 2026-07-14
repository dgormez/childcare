using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// vaccine_types (data-model.md, feature 013g), public schema — shared, platform-wide reference
// data, identical across every tenant (research.md R1). Soft-delete only via IsActive; never
// hard-deleted (existing VaccineRecords may still reference a retired entry).
public class VaccineType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public VaccineCategory? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Feature 013h — deactivation audit trail (FR-008). No DB-level FK on DeactivatedByUserId
    // (research.md R2): the referenced TenantUser lives in an arbitrary tenant's schema, a
    // cross-schema-boundary reference PostgreSQL cannot FK-enforce (same precedent as this
    // entity's own VaccineRecord.VaccineTypeId reference, 013g). DeactivatedByEmail is
    // denormalized at the moment of deactivation so the audit record stays human-readable even
    // if the admin account is later renamed. Invariant: IsActive == true iff all three of these
    // are null; IsActive == false iff all three are populated — never a partial state
    // (data-model.md, FR-008/FR-011).
    public Guid?     DeactivatedByUserId { get; set; }
    public string?   DeactivatedByEmail  { get; set; }
    public DateTime? DeactivatedAt       { get; set; }
}
