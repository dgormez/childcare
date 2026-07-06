using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IProfilePhotoStorage (feature 005-staff) — registered Singleton in
/// OrganisationOnboardingWebAppFactory, overriding Program.cs's real GcsProfilePhotoStorage, so
/// tests never hit real GCS (constitution Principle V — this is an external-service seam, faked
/// the same way Google/Apple OAuth validation already is, feature 003, research.md R3).
/// Deterministic, non-expiring "signed" URLs so tests can assert on them without a real signer.
/// </summary>
public class FakeProfilePhotoStorage : IProfilePhotoStorage
{
    public Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid staffProfileId, CancellationToken cancellationToken = default)
    {
        var objectPath = $"staff/{staffProfileId}/photo.jpg";
        return Task.FromResult((objectPath, $"https://fake-gcs.test/upload/{objectPath}"));
    }

    public Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(objectPath is null ? null : $"https://fake-gcs.test/download/{objectPath}");
}
