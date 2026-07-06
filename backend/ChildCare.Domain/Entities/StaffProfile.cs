using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class StaffProfile
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public Guid   TenantUserId { get; set; }
    public string FirstName    { get; set; } = string.Empty;
    public string LastName     { get; set; } = string.Empty;
    public string Phone        { get; set; } = string.Empty;

    // Required when the linked TenantUser.Role == Staff, optional when Role == Director
    // (FR-003) — enforced by the validator, not the schema (research.md R7).
    public QualificationLevel? QualificationLevel { get; set; }

    // GCS object path only, never a URL (research.md R3) — signed download URLs are generated
    // fresh on every read.
    public string? ProfilePhotoObjectPath { get; set; }

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
