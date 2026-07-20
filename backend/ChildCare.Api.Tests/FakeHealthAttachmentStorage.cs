using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IHealthAttachmentStorage (feature 013c, research.md R2) — registered
/// Singleton in OrganisationOnboardingWebAppFactory, overriding Program.cs's real
/// GcsHealthAttachmentStorage, so tests never hit real GCS. Mirrors FakeProfilePhotoStorage's
/// deterministic, non-expiring "signed" URL pattern.
/// </summary>
public class FakeHealthAttachmentStorage : IHealthAttachmentStorage
{
    // 031-photo-lifecycle-governance test seam — mirrors FakeExpoPushSender's ThrowOnSend
    // pattern for simulating a partial purge failure.
    public bool ThrowOnDelete { get; set; }
    public HashSet<string> DeletedPaths { get; } = [];
    public Dictionary<string, string> StorageClasses { get; } = [];

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => "bin",
    };

    public Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid healthRecordId, string contentType, string category = "health-records", CancellationToken cancellationToken = default)
    {
        var objectPath = $"{category}/{healthRecordId}/attachment.{ExtensionFor(contentType)}";
        return Task.FromResult((objectPath, $"https://fake-gcs.test/upload/{objectPath}"));
    }

    public Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(objectPath is null ? null : $"https://fake-gcs.test/download/{objectPath}");

    public Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-gcs.test/download/{objectPath}?attachment={Uri.EscapeDataString(downloadFileName)}");

    public Task<bool> DeleteAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        if (DeletedPaths.Contains(objectPath))
            return Task.FromResult(true);
        if (ThrowOnDelete)
            return Task.FromResult(false);
        DeletedPaths.Add(objectPath);
        return Task.FromResult(true);
    }

    public Task SetStorageClassAsync(string objectPath, string storageClass, CancellationToken cancellationToken = default)
    {
        StorageClasses[objectPath] = storageClass;
        return Task.CompletedTask;
    }
}
