namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for staff profile photos (research.md R3) — the API never proxies image
/// bytes. ProfilePhotoObjectPath stores only the GCS object path; a download URL is generated
/// fresh on every read so it can never outlive its intended short lifetime.
/// </summary>
public interface IProfilePhotoStorage
{
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid staffProfileId, CancellationToken cancellationToken = default);

    /// <summary>Returns null when objectPath is null (no photo set) rather than signing a meaningless URL.</summary>
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);
}
