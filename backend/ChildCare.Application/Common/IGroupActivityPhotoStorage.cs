namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for group-activity photos (research.md R2/R3) — a separate port from
/// IProfilePhotoStorage, not an extension of it: that port's contract guarantees exactly one
/// deterministic object per (category, subjectId); group activities need zero-to-ten
/// independently addressable, resized photos per activity, a genuinely different shape.
/// Unlike IProfilePhotoStorage, the API resizes bytes in-process (research.md R3 — no signed
/// client-side PUT for this port) and writes directly to GCS using its own credentials.
/// </summary>
public interface IGroupActivityPhotoStorage
{
    /// <summary>
    /// Resizes <paramref name="imageBytes"/> to a max 1920px long edge, generates a 400px
    /// thumbnail, and uploads both to GCS. Returns the full-image and thumbnail object paths.
    /// </summary>
    Task<(string ObjectPath, string ThumbnailObjectPath)> UploadAsync(
        Guid groupActivityId, Guid photoId, Stream imageBytes, CancellationToken cancellationToken = default);

    /// <summary>Returns null when objectPath is null (no photo set) rather than signing a meaningless URL.</summary>
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes both the full-image and thumbnail GCS objects for one photo. Best-effort: a
    /// failure is logged and reported via the boolean return rather than thrown, mirroring
    /// IProfilePhotoStorage.DeleteAsync's semantics (031-photo-lifecycle-governance FR-016) so a
    /// purge cascade can attempt every object and aggregate failures instead of aborting on the
    /// first error. Returns true only if both objects no longer exist afterward (including if
    /// either never existed — idempotent, safe to retry).
    /// </summary>
    Task<bool> DeleteAsync(string objectPath, string thumbnailObjectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a time-limited download URL with an attachment (not inline) content-disposition, so
    /// the browser/app saves the file rather than rendering it (031-photo-lifecycle-governance
    /// FR-013) — always points at the full-resolution object, never the thumbnail.
    /// </summary>
    Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the object to <paramref name="storageClass"/> (e.g. "NEARLINE", "COLDLINE")
    /// in place — no-op if it is already on that class (031-photo-lifecycle-governance R2/R5).
    /// </summary>
    Task SetStorageClassAsync(string objectPath, string storageClass, CancellationToken cancellationToken = default);
}
