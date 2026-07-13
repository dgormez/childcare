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
}
