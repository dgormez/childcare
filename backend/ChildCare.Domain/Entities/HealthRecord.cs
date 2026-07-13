using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// health_records (data-model.md, feature 013c) — the structured counterpart to Child's free-text
// medical fields (feature 006). AttachmentObjectPath is a GCS object path, never a URL — a
// signed download URL is generated fresh on every read via IHealthAttachmentStorage.
public class HealthRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    public HealthRecordType RecordType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? AttachmentObjectPath { get; set; }

    public Guid? RecordedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Soft-delete: null = active.
    public DateTime? DeletedAt { get; set; }
}
