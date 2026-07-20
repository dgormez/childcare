using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IProfilePhotoStorage (feature 005-staff, generalized in feature 006-children
/// research.md R1) — registered Singleton in OrganisationOnboardingWebAppFactory, overriding
/// Program.cs's real GcsProfilePhotoStorage, so tests never hit real GCS (constitution
/// Principle V — this is an external-service seam, faked the same way Google/Apple OAuth
/// validation already is, feature 003, research.md R3). Deterministic, non-expiring "signed"
/// URLs so tests can assert on them without a real signer.
/// </summary>
public class FakeProfilePhotoStorage : IProfilePhotoStorage
{
    // 031-photo-lifecycle-governance test seam — mirrors FakeExpoPushSender's ThrowOnSend
    // pattern for simulating a partial purge failure.
    public bool ThrowOnDelete { get; set; }
    public HashSet<string> DeletedPaths { get; } = [];
    public Dictionary<string, string> StorageClasses { get; } = [];

    public Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(string category, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var objectPath = $"{category}/{subjectId}/photo.jpg";
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
