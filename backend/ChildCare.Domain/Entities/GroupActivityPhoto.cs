namespace ChildCare.Domain.Entities;

// group_activity_photos (data-model.md) — zero-to-ten photos per GroupActivity. Composition:
// no independent lifecycle outside the parent activity (ON DELETE CASCADE in the DB; GCS objects
// are deleted explicitly by DeleteGroupActivityCommand, since GCS isn't transactional with
// Postgres — research.md/data-model.md).
public class GroupActivityPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupActivityId { get; set; }

    // GCS object paths (never public URLs) — resized full image + generated thumbnail
    // (research.md R2/R3).
    public string ObjectPath { get; set; } = string.Empty;
    public string ThumbnailObjectPath { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
