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
}
