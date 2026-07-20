namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for profile photos (research.md R3, feature 005; generalized to
/// (category, subjectId) in feature 006's research.md R1 so staff and child photos share one
/// mechanism) — the API never proxies image bytes. The object path stores only the GCS
/// location; a download URL is generated fresh on every read so it can never outlive its
/// intended short lifetime.
/// </summary>
public interface IProfilePhotoStorage
{
    /// <param name="category">A short, stable path segment distinguishing the subject type (e.g. "staff", "children") so different features' photos never collide in the same bucket.</param>
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(string category, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>Returns null when objectPath is null (no photo set) rather than signing a meaningless URL.</summary>
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a time-limited download URL with an attachment (not inline) content-disposition, so
    /// the browser/app saves the file rather than rendering it (031-photo-lifecycle-governance
    /// FR-013) — always points at the full-resolution object, never a thumbnail.
    /// </summary>
    Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the GCS object outright (031-photo-lifecycle-governance FR-007/FR-015 — GDPR
    /// purge). Best-effort: a failure is logged and reported to the caller via a boolean return
    /// rather than thrown, mirroring IGroupActivityPhotoStorage.DeleteAsync's existing semantics
    /// so a purge cascade can attempt every object and aggregate failures instead of aborting on
    /// the first error. Returns true if the object no longer exists afterward (including if it
    /// never existed — idempotent, safe to retry).
    /// </summary>
    Task<bool> DeleteAsync(string objectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the object to <paramref name="storageClass"/> (e.g. "NEARLINE", "COLDLINE")
    /// in place — no-op if it is already on that class (031-photo-lifecycle-governance R2/R5).
    /// </summary>
    Task SetStorageClassAsync(string objectPath, string storageClass, CancellationToken cancellationToken = default);
}
