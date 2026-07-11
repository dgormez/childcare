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

    /// <summary>Deletes both the full-image and thumbnail GCS objects for one photo (best-effort — errors are logged, never thrown, since a missing object shouldn't block a DB delete).</summary>
    Task DeleteAsync(string objectPath, string thumbnailObjectPath, CancellationToken cancellationToken = default);
}
