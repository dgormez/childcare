using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IGroupActivityPhotoStorage (feature 009b) — registered Singleton in
/// OrganisationOnboardingWebAppFactory, overriding Program.cs's real GcsGroupActivityPhotoStorage,
/// so tests never hit real GCS or need Application Default Credentials (constitution Principle V
/// — external-service seam, faked the same way FakeProfilePhotoStorage already fakes GCS for
/// staff/child photos). Deterministic, non-expiring "signed" URLs so tests can assert on them.
/// </summary>
public class FakeGroupActivityPhotoStorage : IGroupActivityPhotoStorage
{
    // 031-photo-lifecycle-governance test seam — mirrors FakeExpoPushSender's ThrowOnSend
    // pattern for simulating a partial purge failure.
    public bool ThrowOnDelete { get; set; }
    public HashSet<string> DeletedPaths { get; } = [];
    public Dictionary<string, string> StorageClasses { get; } = [];

    public Task<(string ObjectPath, string ThumbnailObjectPath)> UploadAsync(
        Guid groupActivityId, Guid photoId, Stream imageBytes, CancellationToken cancellationToken = default)
    {
        var objectPath = $"group-activities/{groupActivityId}/{photoId}.jpg";
        var thumbnailObjectPath = $"group-activities/{groupActivityId}/{photoId}-thumb.jpg";
        return Task.FromResult((objectPath, thumbnailObjectPath));
    }

    public Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(objectPath is null ? null : $"https://fake-gcs.test/download/{objectPath}");

    public Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-gcs.test/download/{objectPath}?attachment={Uri.EscapeDataString(downloadFileName)}");

    public Task<bool> DeleteAsync(string objectPath, string thumbnailObjectPath, CancellationToken cancellationToken = default)
    {
        if (ThrowOnDelete && !DeletedPaths.Contains(objectPath))
            return Task.FromResult(false);
        DeletedPaths.Add(objectPath);
        DeletedPaths.Add(thumbnailObjectPath);
        return Task.FromResult(true);
    }

    public Task SetStorageClassAsync(string objectPath, string storageClass, CancellationToken cancellationToken = default)
    {
        StorageClasses[objectPath] = storageClass;
        return Task.CompletedTask;
    }
}
