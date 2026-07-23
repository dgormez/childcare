using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// staff_documents (data-model.md, feature 028) — one HR document per staff member. Soft-deleted
// (DeletedAt/DeletedBy) rather than hard-deleted so the FR-012a audit trail survives removal —
// mirrors this codebase's dominant DeactivatedAt idiom (StaffProfile, Location). The underlying
// GCS object is still hard-deleted via IStaffDocumentStorage.DeleteAsync.
public class StaffDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }

    public StaffDocumentType DocumentType { get; set; }
    public string Title { get; set; } = string.Empty;

    // GCS object path only, never a URL (research.md R3) — signed download URLs are generated
    // fresh on every read.
    public string ObjectPath { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
