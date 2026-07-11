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
    public Task<(string ObjectPath, string ThumbnailObjectPath)> UploadAsync(
        Guid groupActivityId, Guid photoId, Stream imageBytes, CancellationToken cancellationToken = default)
    {
        var objectPath = $"group-activities/{groupActivityId}/{photoId}.jpg";
        var thumbnailObjectPath = $"group-activities/{groupActivityId}/{photoId}-thumb.jpg";
        return Task.FromResult((objectPath, thumbnailObjectPath));
    }

    public Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(objectPath is null ? null : $"https://fake-gcs.test/download/{objectPath}");

    public Task DeleteAsync(string objectPath, string thumbnailObjectPath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
